# ASP.NET Core adapter

`Shiron.Backflow.Ext.AspNetCore` is an isolated, .NET 10 thin HTTP adapter. The
execution core and nodes remain usable without ASP.NET Core. It has no application,
storage, editor or frontend dependencies. Its only runtime framework dependency is
`Microsoft.AspNetCore.App`; node activation reuses `Backflow.Ext.DI`.

## Register and map

```csharp
using Shiron.Backflow.Ext.AspNetCore;

builder.Services.AddAuthentication(/* host authentication configuration */);
builder.Services.AddAuthorization();
builder.Services.AddBackflow((services, catalog) => {
    catalog.Add<MyNode>("my-node");
}, options => {
    options.AuthorizationPolicy = "graph-authoring";
    options.MaximumNodes = 128;
    options.MaximumEdges = 512;
    // EnableExecution is false by default.
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapBackflow("/api/backflow");
```

Supply real host authentication and register the named authorization policy. Without
a named policy, the host's default authorization policy applies. Anonymous access
requires explicit `AllowAnonymous = true`. The returned `RouteGroupBuilder` also
supports normal ASP.NET Core endpoint conventions, such as host rate limiting.

Register an existing `NodeRegistry` singleton **before** `AddBackflow` to reuse it.
`catalog.Add<TNode>(id)` reuses its registered instance or creates one through its
DI activator. `catalog.Add(id, node)` requires that exact registered instance.
Only published nodes are visible, even if the host registry contains other nodes.
Configure registrations at startup; do not mutate published node ports at runtime.
Nodes are shared declarations and must not retain per-execution mutable state.

## Non-HTTP compiler and declaration-only catalogs

Hosts with an existing document, canonical hash and persistence format can use
the exact same compiler directly without changing those formats or introducing
fake executable nodes:

```csharp
builder.Services.AddBackflow((_, catalog) => {
    catalog.AddContract<SourceConfiguration>("source", ports => ports.Output<Payload>("value"));
    catalog.AddContract<SinkConfiguration>("sink", ports => ports.Input<Payload>("value"));
});

// Inject BackflowGraphService into the host's existing graph service.
var topology = new BackflowGraph(
    [new BackflowNode("source-instance", "source"), new BackflowNode("sink-instance", "sink")],
    [new BackflowEdge("source-instance", "value", "sink-instance", "value")]);
if (compiler.TryCompile(topology, out var compiled, out var validation, cancellationToken)) {
    // compiled.TopologicalNodeIds is the validated DAG order.
    // Persist/hash the ORIGINAL host document using the existing host rules.
}
```

`SourceConfiguration`, `SinkConfiguration` and `Payload` above are host-defined
types. Configuration schemas and port schemas are derived from those types, never
hand-written JSON contracts. Configuration values remain in the host's typed
document; this adapter does not rewrite or validate the host's configuration fields
or own its canonicalization. Hosts map only node IDs, catalog IDs and connections
to `BackflowGraph`. `TryCompile` preserves that topology and checks it using the
same shared DAG implementation as the execution core. It performs no execution,
node construction, persistence or hashing. `ArrayInput<T>` supports indexed array
ports with declared cardinality.

`GetCatalog()`, `Validate(...)`, `TryCompile(...)`, `TryBuild(...)` and
`TryPrepareExecution(...)` are public non-HTTP methods on the DI singleton
`BackflowGraphService`; HTTP routes are optional. `/graphs` and `/validate` use the
same non-executing compiler. `TryBuild` additionally materializes an executable
pipeline and requires every entry to have a real registered node implementation.
Declaration-only contracts have `IsExecutable = false` in the catalog and return
`NotExecutable` if execution is attempted, even when execution endpoints are enabled.
There is no noop implementation or hidden fallback executor.

## HTTP contract

All paths below are relative to the chosen route prefix. Request/response types are
public C# DTOs and are included in endpoint metadata for host OpenAPI generation.
Enums use the host's normal JSON serialization configuration (numeric by default).

| Method | Path | Request | Success response |
|---|---|---|---|
| GET | `/catalog` | — | `BackflowCatalogDescriptor` |
| POST | `/validate` | `BackflowGraph` | `BackflowValidationResult` |
| POST | `/graphs` | `BackflowGraph` | `BackflowGraphResult` |
| POST | `/executions` | `BackflowExecutionSubmission` | `BackflowExecutionAccepted` (202), **opt-in only** |

`/graphs` is stateless creation/compilation validation, **not a save endpoint**. It returns
the accepted topology, preserving client IDs; it does not allocate a persistent ID
or advertise a resource location. No default in-memory store, save/load route or
implicit authoritative persistence is installed. Hosts can compose
`BackflowGraphService.TryCompile` with their own persistence and permissions.
Keep editor coordinates, selection, layout and other presentation data outside
`BackflowGraph` and store them separately from executable topology.

```json
{
  "nodes": [
    { "id": "first", "catalogId": "math.add" },
    { "id": "second", "catalogId": "math.add" }
  ],
  "edges": [
    {
      "sourceNodeId": "first", "sourcePort": "sum",
      "destinationNodeId": "second", "destinationPort": "left"
    }
  ]
}
```

The catalog reports actual CLR port types and JSON schemas generated using
`System.Text.Json.Schema`, not a manually maintained command/type dictionary.
`ValueSerializerOptions` controls both schemas and literal decoding; its copy is
frozen during registration. Use the same host-owned converters/resolver for both.
Array-input cardinality and required flags come from the actual port declarations.
Schema metadata describes serialization shape, not arbitrary node runtime validators.

Validation rejects unpublished identifiers, duplicate node IDs, missing endpoints,
incorrect port directions, mismatched port types, multiple writers, cycles and
invalid indexed-array cardinalities. Connections require **exact CLR type equality**;
implicit numeric/string conversions are deliberately not enabled at this boundary.
Hosts may publish explicit conversion nodes. Open generic types cannot be requested
or inferred over HTTP: publish each permitted, already-closed generic node variant
under its own catalog ID.

Failures return `BackflowValidationResult` with typed `BackflowErrorCode` errors.
Invalid graphs/bodies return 400, byte-limit violations 413 and non-JSON content
415. Node/edge/literal counts, identifier lengths, array lengths/indices, JSON depth
and request body bytes are configurable and positive. Body bounds apply to streamed
requests without Content-Length as well. Validation is fail-fast and returns one
diagnostic, bounding response size. Cancellation follows `RequestAborted`.
The host must additionally configure transport timeouts, rate limits and overall
concurrency quotas; per-request bounds alone do not prevent aggregate exhaustion.

## Optional execution admission

Register a real `IBackflowExecutionScheduler` and set `EnableExecution = true`.
Enabling the route without a scheduler fails at mapping/startup. With execution off,
`/executions` does not exist. No adapter code invokes `PipelineExecutor` or creates
a second execution path behind the host's job system.

The scheduler receives `BackflowExecutionRequest`: the accepted graph, a built
`Pipeline` preserving node IDs and a fresh `ArrayPipelineContext`. Literal values
are decoded **only** as their target input's declared CLR type; no literal carries
a runtime type selector. Literals may only write unique, unconnected input ports.
Array bounds/cardinality and serialized shapes are checked before admission;
node-specific value validators remain enforced by the normal port reads at runtime.

```json
{
  "graph": { "nodes": [{ "id": "one", "catalogId": "math.add" }], "edges": [] },
  "inputs": [
    { "nodeId": "one", "portName": "left", "value": 3 },
    { "nodeId": "one", "portName": "right", "value": 4 }
  ]
}
```

The host owns admission permissions/quotas, persistence, durable scheduling,
execution cancellation, job status and output serialization. Admission cancellation
uses the request token; accepted jobs need a host-owned lifetime, not that token.
Never retain request-scoped services in a background job. A host scheduler should
persist or copy the graph/literals into its job contract before acknowledging
durable work. Its receipt supplies `JobId`; hosts define their own status/results
routes. Host admission exceptions should be translated by the host's exception
middleware. This adapter deliberately does not invent job states or storage.

## Trust boundary

Clients select only catalog IDs and declared port names. The adapter translates IDs
to a private concrete registry snapshot and never calls `Type.GetType`, scans
assemblies or instantiates generic types from request fields. It does not accept
paths, scripts, node code or arbitrary converter names. The core's general-purpose
`PipelineSerialization` runtime-type input APIs are **not exposed** over HTTP;
the topology reader is called only after whitelist/direction/type/bounds checks,
with internally supplied concrete node names and no generic arguments.

The host is responsible for the behavior of published nodes, their constructors,
custom serializers and converters. Publishing a node is explicitly granting its
capability; this is not a sandbox for untrusted executable node implementations.
Declare data-only port DTOs and avoid dangerous polymorphic deserializers.

## Runnable sample and tests

```bash
dotnet run --project samples/Backflow.AspNetCore --urls http://127.0.0.1:5099
curl http://127.0.0.1:5099/api/graphs/catalog
curl -H 'Content-Type: application/json' --data '{"nodes":[{"id":"one","catalogId":"math.add"}],"edges":[]}' http://127.0.0.1:5099/api/graphs/validate
```

The local-only sample explicitly permits anonymous authoring and publishes a real
integer addition node. Execution is disabled: a production host must provide its
own actual scheduling implementation, rather than inheriting a sample queue.

```bash
dotnet test tests/Backflow.Tests.Ext.AspNetCore --configuration Release
```

Graph semantics use unit tests. TestServer integration is limited to HTTP behaviors
that unit tests cannot establish: routing, middleware authorization, streaming byte
bounds, request decoding and host-scheduler dispatch.
