# @shiron/backflow-react

An independently usable React 19 / ReactFlow DAG editor for the real
`Backflow.Ext.AspNetCore` catalog. **AGPL-3.0-only**. No application-specific API,
storage, navigation, theme, scheduler, or node implementation is embedded.

## Install and render

```sh
pnpm add @shiron/backflow-react @xyflow/react react@19 react-dom@19
```

```tsx
import { useState } from "react";
import {
    BackflowEditor,
    createDocument,
    type BackflowCatalogDescriptor,
} from "@shiron/backflow-react";
import "@xyflow/react/dist/style.css";

export function Editor({ catalog }: { catalog: BackflowCatalogDescriptor }) {
    const [document, setDocument] = useState(createDocument);
    return (
        <div style={{ height: 600 }}>
            <BackflowEditor
                catalog={catalog}
                document={document}
                onChange={setDocument}
            />
        </div>
    );
}
```

Fetch the catalog with `createBackflowClient({ baseUrl: "/api/backflow" }).catalog(signal)`.
Add published nodes using `addNode(document, catalog, catalogId, crypto.randomUUID(), position)`.
Do not invent an empty catalog entry while loading: load a document and its catalog
before mounting the editor. An unpublished node throws a migration/reload error;
hosts should provide an error boundary. Unknown catalog entries are never fabricated.

The host provides all toolbars and configuration/literal forms. `children` accepts
ReactFlow overlays/panels, and `onInit` exposes its typed instance for zoom, fit,
screen-to-flow positioning, and host drag/drop. This package creates no buttons,
inputs, or UI-library overrides. Hosts can compose HonamiUI controls without changing
its default theme. The only intrinsic UI is the ReactFlow graph canvas and its port labels.

## Controlled API

- `BackflowEditor`: `document`, `catalog`, `onChange?`, `readOnly?`.
  Missing `onChange` disables graph modifications. Dragging and deletion update
  the supplied document immutably; connected edges and metadata are cleaned when
  a node is deleted. Node/edge deletions are applied atomically.
- `selectedNodeIds?` / `onSelectionChange?` and `selectedEdgeIds?` /
  `onEdgeSelectionChange?`: controlled selection. Omit IDs for local selection.
  Read-only canvases still permit selection, zoom and pan, but not edits.
- `renderNode({ id, descriptor, selected })`: fully typed custom node content.
  It does not replace the port handles. `renderPort(port, index, nodeContext)` customizes each
  label, keeping the real catalog handle identities/directions intact. Use closures
  to read host configuration, select a node, or render host-owned schema forms.
- `arrayIndices(nodeId, port)`: additional indexed-array slots to expose. The
  canvas always includes the whole-port handle, connected indices and the next
  index. It never expands an arbitrarily large declared maximum into DOM nodes.
- `portStyle(port, index, nodeContext)` applies typed React CSS properties to the
  actual ReactFlow handle. `edgeStyle(edge, sourcePort)` applies them to the edge.
  Use these rendering-boundary callbacks to convert host color abstractions into
  CSS, not CSS selector overrides or alternate domain color representations.
- `viewport` / `onViewportChange` provides a controlled ReactFlow viewport;
  `defaultViewport` / `onMoveEnd` supports host persistence without controlling
  every frame. `fitView` is configurable and defaults to **false** when either
  viewport prop is present, otherwise true. Viewport metadata stays host-owned,
  separate from executable topology and the default document codec.

```tsx
<BackflowEditor
    catalog={catalog}
    document={document}
    onChange={setDocument}
    viewport={hostViewport}
    onViewportChange={setHostViewport}
    onMoveEnd={(_event, viewport) => persistHostViewport(viewport)}
    fitView={false}
    portStyle={(port) => ({ background: hostPortCssColor(port) })}
    edgeStyle={(_edge, sourcePort) => ({
        stroke: hostPortCssColor(sourcePort),
    })}
/>
```

The color conversion and viewport persistence functions above belong to the host;
no color contract or persistence endpoint is invented by this library.

The public `BackflowDocument` is editor data, **not** an ASP wire DTO:

```ts
interface BackflowDocument {
    graph: BackflowGraph; // generated C# topology DTO
    inputs: BackflowInput[]; // generated C# literal DTO
    positions: Record<string, { x: number; y: number }>;
    configuration: Record<string, JsonValue>; // host-owned per-node data
}
```

`configuration` can retain host metadata and options. Its shape is described by
the catalog's generated `configurationSchema`; the host owns its form, validation,
canonicalization and persistence. The ASP compiler does not consume or rewrite it.
There is deliberately no hand-maintained TypeScript node/options dictionary.

Pure immutable helpers: `createDocument`, `addNode`, `removeNode`, `connect`,
`canConnect`, `setLiteral`, `portDescriptor`, `edgeId`. Port metadata includes actual
CLR identity, direction, required flags, array cardinality and generated JSON schema.
`setLiteral` prevents connected/unknown input writes; the host performs authoritative
literal shape decoding during execution admission. A graph validation response does
not mean that runtime literals or arbitrary host configuration have been validated.

`parseDocument` / `serializeDocument` provide structurally validated JSON load/save.
Persist that document through your own storage API, or adapt these operations into
your existing document format. Never treat `client.build()` as a save operation.

## ASP validation, compilation and optional execution

All exports from `@shiron/backflow-client`, including generated contracts, are
re-exported. See its [client README](../backflow-client/README.md).

```ts
const client = createBackflowClient({ baseUrl: "/api/backflow" });
const validation = await client.validate(
    document.graph,
    abortController.signal,
);
const compiled = await client.build(document.graph, abortController.signal);
// compiled.graph preserves client IDs. No persistence or execution occurred.
```

Execution is **separate and opt-in**. Use `createBackflowExecutionClient` only when
the actual ASP host enables its route and registers a real scheduler. Submit the
topology and `document.inputs`; consume the real returned `jobId`. Job state,
results, cancellation after admission and host configuration mapping remain host APIs.

## Host migration notes

- Keep your authoritative graph document and canonical hash unchanged. Map only
  node IDs, catalog IDs, named ports and connections to generated `BackflowGraph`.
  Do not replace an existing typed domain document with this editor's JSON envelope.
- Node IDs are opaque strings, not generated server identifiers. UUID strings are
  preserved verbatim; no numeric coercion, UUID normalization or reallocation occurs.
- CLR identity matching is exact. Indexed destinations target array _element_
  types; whole-array connections require the array type itself. No implicit coercions
  or generic inference are offered. `destinationIndex: null` is a whole-port write;
  integer indices are zero-based. Whole-array/indexed writes cannot mix, and a slot
  cannot have multiple writers. ASP validates final cardinality and its resource bounds.
- `canConnect` is optimistic UX validation, not a replacement for ASP validation.
  The server additionally enforces quotas, cardinality, whitelists and host serializers.
- JS numbers cannot losslessly represent arbitrary Int64/UInt64 integers or decimal
  quantities. Coordinates and indices use finite JS numbers. Preserve precision-sensitive
  domain values in your original host representation; do not round-trip them through
  `JSON.parse`, `JsonValue` numbers or this generic persistence codec. Use host DTOs
  and configured ASP value converters for documented string encodings where appropriate.
- Schema values/configuration are derived from actual C# types at runtime. Literal
  values contain no client-selected runtime CLR type. Never synthesize node runtime
  types or execute unpublished nodes.
- Keep application tags, galleries, jobs, routing, permissions and storage in the host.
  The canvas changes neither URL routing nor any application UI library's theme.

## Run the real sample

From the repository root (after initializing `external/lib`):

```sh
pnpm install
pnpm generate
pnpm build
dotnet run --project samples/Backflow.AspNetCore --urls http://127.0.0.1:5099
# In another terminal:
pnpm --filter backflow-react-sample dev
```

Open Vite's printed URL. Drag a real published addition node onto the canvas and
connect ports. Ctrl/⌘ S saves the full document locally; Ctrl/⌘ Enter calls actual
ASP validation; Ctrl/⌘ B calls actual topology compilation. Reload restores local
data. Execution remains disabled in the ASP sample: there is no sample fake queue.

## Development

`pnpm build`, `pnpm typecheck`, `pnpm test`, `pnpm format`. Nx infers package targets
from their scripts/configuration; no per-project `project.json` is needed. TypeScript
5.9 is used because the installed Nx plugin requires its compiler API (TS 7 changed it).
Tests cover document invariants, actual ReactFlow mounting/selection and HTTP contract
compatibility against the real ASP sample, not an invented frontend-only contract.
