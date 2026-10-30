# @shiron/backflow-client

Headless, React-independent HTTP client for `Backflow.Ext.AspNetCore`. **MIT**.
The React editor is separately licensed **AGPL-3.0-only**.

```sh
pnpm add @shiron/backflow-client
```

```ts
import {
    createBackflowClient,
    BackflowHttpError,
} from "@shiron/backflow-client";

const client = createBackflowClient({
    baseUrl: "/api/backflow",
    fetch: globalThis.fetch, // optional: supply your host's transport
    requestInit: async () => ({
        credentials: "include",
        headers: { Authorization: `Bearer ${await refreshHostAccessToken()}` },
    }),
});
const controller = new AbortController();
const catalog = await client.catalog(controller.signal);
const graph = { nodes: [], edges: [] };
const validation = await client.validate(graph, controller.signal);
const result = await client.build(graph, controller.signal);
```

`refreshHostAccessToken` above is host-owned. Authentication is read for each
request. Request cancellation and transport errors propagate unchanged. HTTP
failures throw `BackflowHttpError` with `status` and the original JSON/text `body`;
only a schema-valid HTTP 400 validation result is returned by `validate` as data.
Invalid successful response shapes fail instead of silently masquerading as DTOs.

`catalog`, `validate` and `build` map to `/catalog`, `/validate` and `/graphs`.
`build` is **stateless compilation**, not persistence or executable materialization.
It returns the accepted graph with unchanged client IDs; it neither saves nor runs it.

## Optional host execution

```ts
import { createBackflowExecutionClient } from "@shiron/backflow-client";

const execution = createBackflowExecutionClient({
    baseUrl: "/api/backflow",
    executionEnabled: true,
    // Supply the same host authentication/transport options here.
});
const receipt = await execution.submit(
    { graph, inputs: [] },
    controller.signal,
);
// receipt.jobId comes from the real host scheduler's HTTP 202 response.
```

Enable this only after the ASP host enables execution **and registers a real
`IBackflowExecutionScheduler`**. `client.withExecution(options)` provides the same
explicitly configured boundary. Disabled routes fail normally (404); no browser
executor, fake job, invented state API or implicit polling exists. Scheduling receipts
preserve opaque `jobId` strings; status/results and durable cancellation are host APIs.

## Contract generation and architecture

- C# Backflow core: headless execution DAG and node declarations, no frontend dependency.
- ASP adapter: published catalog/schema, validation/compilation, optional scheduler admission.
- This package: generated wire DTOs, exported `backflowSchemas`, checked HTTP transport.
- React package: controlled presentation/document operations; no hidden persistence/execution.

From the repository root, `pnpm generate` runs `tools/Backflow.TypeScript`. It reflects
the **actual adapter assembly** to emit `src/contracts.ts` and `src/schemas.json`
using `System.Text.Json.Schema`; there are no handwritten copies of DTO properties,
port discriminators or error enums. The server-only execution request/pipeline context
is deliberately not an HTTP contract. Generated files are committed so consumers need
neither .NET nor a generator at runtime. Run generation and verify no diff when changing C# DTOs.

The default adapter's numeric enum JSON serialization is required by these generated
contracts. A host that switches endpoint enums to strings must generate corresponding
contracts/schemas; arbitrary host serialization changes are not silently guessed.
Port/literal JSON schemas themselves still come from the host's configured value
serializer. No client-supplied CLR type is ever resolved.

The codec uses standard JSON/JS numbers; retain precision-sensitive Int64/decimal host
values in their original representation or an explicitly configured string encoding.
Graph IDs remain strings. The HTTP client does not own host configuration, node options,
presentation, authorization rules, canonical hashes, storage or job infrastructure.

`pnpm test` includes a real ASP-host compatibility test (catalog metadata, JSON schemas,
validation diagnostics, topology compilation, and disabled execution). Integration is
necessary for the actual CLR/HTTP serialization boundary; cheaper client behavior is
covered with isolated fetch doubles. No proprietary generation API is contacted.
