import { StrictMode, useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import {
    BackflowPortDirection,
    type BackflowCatalogDescriptor,
} from "@shiron/backflow-client";
import type { ReactFlowInstance, Edge } from "@xyflow/react";
import {
    BackflowEditor,
    type BackflowCanvasNode,
} from "../../src/BackflowEditor.js";
import {
    addNode,
    createDocument,
    connect,
    removeNode,
    type BackflowDocument,
} from "../../src/document.js";
import "@xyflow/react/dist/style.css";

// These declarations are test-owned: no API/inference is simulated by production code.
const catalog: BackflowCatalogDescriptor = {
    nodes: [
        {
            id: "browser.fixture",
            isExecutable: false,
            configurationSchema: null,
            ports: [
                {
                    name: "input",
                    direction: BackflowPortDirection.Input,
                    clrType: "System.Int32",
                    schema: { type: "integer" },
                    required: false,
                    minimumCount: null,
                    maximumCount: null,
                },
                {
                    name: "output",
                    direction: BackflowPortDirection.Output,
                    clrType: "System.Int32",
                    schema: { type: "integer" },
                    required: false,
                    minimumCount: null,
                    maximumCount: null,
                },
            ],
        },
        {
            id: "browser.special",
            isExecutable: false,
            configurationSchema: null,
            ports: [
                {
                    name: 'input "quoted" [slot] \\ %',
                    direction: BackflowPortDirection.Input,
                    clrType: "System.Int32",
                    schema: { type: "integer" },
                    required: false,
                    minimumCount: null,
                    maximumCount: null,
                },
                {
                    name: 'output "quoted" [slot] \\ %',
                    direction: BackflowPortDirection.Output,
                    clrType: "System.Int32",
                    schema: { type: "integer" },
                    required: false,
                    minimumCount: null,
                    maximumCount: null,
                },
            ],
        },
        {
            id: "browser.array",
            isExecutable: false,
            configurationSchema: null,
            ports: [
                {
                    name: "input",
                    direction: BackflowPortDirection.Input,
                    clrType: "System.Int32[]",
                    schema: { type: "array", items: { type: "integer" } },
                    required: false,
                    minimumCount: 0,
                    maximumCount: 4,
                },
                {
                    name: "output",
                    direction: BackflowPortDirection.Output,
                    clrType: "System.Int32",
                    schema: { type: "integer" },
                    required: false,
                    minimumCount: null,
                    maximumCount: null,
                },
            ],
        },
    ],
};
function initialDocument(): BackflowDocument {
    const query = new URLSearchParams(window.location.search);
    const catalogId = query.has("special")
        ? "browser.special"
        : query.has("array")
          ? "browser.array"
          : "browser.fixture";
    return addNode(
        addNode(createDocument(), catalog, catalogId, "first", {
            x: 60,
            y: 80,
        }),
        catalog,
        catalogId,
        "second",
        { x: 420, y: 120 },
    );
}
export interface BrowserHarness {
    document: BackflowDocument;
    instance: ReactFlowInstance<BackflowCanvasNode, Edge> | null;
    documentChanges: number;
    rerender(): void;
    expand(): void;
    removeFirst(): void;
    restoreFirst(): void;
    setReadOnly(value: boolean): void;
    connectFirstToSecond(): void;
    disconnect(): void;
}
declare global {
    interface Window {
        backflowTest: BrowserHarness;
        backflowBrowserErrors: string[];
    }
}

function Fixture() {
    const [document, setDocument] = useState(initialDocument);
    const [instance, setInstance] = useState<ReactFlowInstance<
        BackflowCanvasNode,
        Edge
    > | null>(null);
    const [documentChanges, setDocumentChanges] = useState(0);
    const [revision, setRevision] = useState(0);
    const [expanded, setExpanded] = useState(false);
    const [readOnly, setReadOnly] = useState(false);
    const [selected, setSelected] = useState<string[]>([]);
    useEffect(() => {
        window.backflowTest = {
            document,
            instance,
            documentChanges,
            rerender: () => {
                setRevision((value) => value + 1);
                setDocument((value) => ({
                    ...value,
                    configuration: { first: { refreshed: true } },
                }));
            },
            expand: () => setExpanded((value) => !value),
            removeFirst: () =>
                setDocument((value) => removeNode(value, "first")),
            restoreFirst: () =>
                setDocument((value) =>
                    addNode(value, catalog, "browser.fixture", "first", {
                        x: 60,
                        y: 80,
                    }),
                ),
            setReadOnly,
            connectFirstToSecond: () =>
                setDocument((value) => {
                    const declaration = catalog.nodes.find(
                        (entry) => entry.id === value.graph.nodes[0]?.catalogId,
                    );
                    const output = declaration?.ports.find(
                        (port) =>
                            port.direction === BackflowPortDirection.Output,
                    );
                    const input = declaration?.ports.find(
                        (port) =>
                            port.direction === BackflowPortDirection.Input,
                    );
                    if (!output || !input)
                        throw new Error("Missing fixture ports");
                    return connect(value, catalog, {
                        sourceNodeId: "first",
                        sourcePort: output.name,
                        destinationNodeId: "second",
                        destinationPort: input.name,
                        destinationIndex:
                            input.minimumCount === null ? null : 0,
                    });
                }),
            disconnect: () =>
                setDocument((value) => ({
                    ...value,
                    graph: { ...value.graph, edges: [] },
                })),
        };
    }, [document, instance, documentChanges]);
    return (
        <div style={{ width: 1000, height: 650 }}>
            <BackflowEditor
                document={document}
                catalog={catalog}
                onChange={(value) => {
                    setDocumentChanges((count) => count + 1);
                    setDocument(value);
                }}
                selectedNodeIds={selected}
                onSelectionChange={setSelected}
                onInit={setInstance}
                readOnly={readOnly}
                defaultViewport={{ x: 0, y: 0, zoom: 1 }}
                renderNode={({ id }) => (
                    <div
                        data-testid={`content-${id}`}
                        style={{
                            minHeight: expanded && id === "first" ? 200 : 24,
                        }}
                    >
                        {id} revision {revision}
                    </div>
                )}
            />
        </div>
    );
}
const root = document.getElementById("root");
if (!root) throw new Error("Missing fixture root");
createRoot(root).render(
    <StrictMode>
        <Fixture />
    </StrictMode>,
);
