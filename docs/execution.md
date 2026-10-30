# Reusable execution contracts

Backflow is a standalone .NET graph executor, not an application-specific workflow engine.
Compile a graph once, reuse its node definitions, and supply a separate context per concurrent run.

```csharp
var pipeline = builder.Build();
var executor = new PipelineExecutor(pipeline);
var context = ArrayPipelineContext.ForPipeline(pipeline, new MyOptions(42));
context.Write(inputNode, inputPort, inputValue);
var stats = await executor.ExecuteValueAsync(context, cancellationToken);
var state = context.Execution.GetNodeState(outputNode);
```

## Ownership and isolation

- Node instances describe graph occurrences, not execution state. The previous `NodeInstance.State`
  property is removed: use `context.Execution.GetNodeState(instance)` instead. State remains available
  after completion, cancellation, or failure. Cancellation observed inside a node produces `Canceled`;
  nodes not started remain `Pending`.
- A context owns channels, array-slot masks, lifecycle state, and reusable node-context adapters.
  Overlapping executions of one context throw `InvalidOperationException`, even through different
  executors. Independent contexts may execute the same compiled graph concurrently.
- Sequential reuse resets lifecycle state and clears output channels from the preceding run, including
  channels shared with downstream inputs. Unconnected inputs and directly supplied array slots remain
  caller-owned. The first execution preserves pre-seeded output channels for compatibility. To seed a
  graph differently after execution, write its unconnected inputs or create a fresh context.
- Graph construction snapshots channel mappings, array counts, channel layout, and conversion rules.
  Later builder changes do not alter an already built graph. Executors snapshot scheduling and indexed
  connections. Treat the public `Topology` and `Edges` inspection handles as read-only; do not mutate a
  built graph. `Layers` returns defensive arrays and frozen channel mappings.
- Node definitions freeze their ports, behaviors, required flags, and cache policy when registered or
  added to a builder. Derived nodes, custom ports, validators, conversion delegates, and behaviors must
  themselves be stateless/thread-safe: the engine cannot make arbitrary caller-owned mutable fields
  safe. Use immutable settings and context values rather than storing invocation data on a definition.
- Values written to separate contexts are isolated unless the caller deliberately writes the same
  mutable reference into both. Values are not deep-cloned. Do not retain `INodeContext` after a node
  invocation: its adapter is reused on the next run.

## Options and cache identity

`ArrayPipelineContext.ForPipeline<TOptions>(pipeline, options)` accepts reference-type settings.
Nodes read those settings using `context.GetOptions<TOptions>()`. Settings must be immutable while
the context is in use. There is no process-global current-options or cancellation-token slot.

`ArrayPipelineContext` implements `ICacheIdentityProvider`: its identity is the options object, or the
identity returned by the options' own `ICacheIdentityProvider`. Identity must be deterministic,
JSON-serializable, and include every semantic dependency that affects outputs. A custom cache-key
factory must include node and context identities; deliberately returning null opts out of identity.
Do not put runtime resources, cancellation tokens, or task gates in a serialized identity.

Only successful `Done` nodes write cache entries; behavior-skipped nodes are not cached as successes.
Cache restore uses declared output contracts, never arbitrary `Type.GetType` on persisted type names.
Typed blob metadata must match a metadata type declared by the output contract. Blob reads are
validated before accepting a cache hit; missing/corrupt blobs or incompatible outputs evict the entry
and execute the node normally. Cache implementations shared across runs must support concurrency.

## Scheduling and performance

`Execute` runs nodes sequentially. `ExecuteAsync` and `ExecuteValueAsync` start independent nodes
within a layer directly, overlapping incomplete asynchronous operations and joining **all** started
operations before releasing the context, including on failure. They no longer use `Task.Run` per
node. Synchronous CPU-bound nodes therefore execute inline rather than in parallel on worker threads;
hosts needing CPU parallelism should schedule whole independent executions explicitly.

Cancellation is cooperative and passed to every node invocation. The token is checked before nodes
and between layers, including after the final layer. Await asynchronous nodes through the asynchronous
APIs; the synchronous API blocks and should not be used from a synchronization context whose thread
is needed by node implementations.

The warmed synchronous typed path reuses node adapters and state arrays. It avoids per-node tasks,
stopwatch objects, runtime indexed-connection dictionaries, and boxing for typed numeric conversions.
`ExecuteValueAsync` can complete without a task allocation. `ExecuteAsync` retains its `Task` return
type for compatibility and allocates a result task. Compilation and initial context setup allocate;
caching, events, arrays, custom untyped conversions, and real asynchronous work can also allocate.
See [the measured benchmark report](../BENCHMARKS.md); there is no general zero-allocation claim.

Custom `IPipelineContext` implementations must provide a distinct `PipelineExecutionState Execution`
and implement `Clear(channel)` to remove presence and old values. `GetOptions<TOptions>()` has a
default null implementation. Implementations must permit independent nodes to access distinct
channels concurrently and must not share mutable storage with other execution contexts.
