import { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import {
    BackflowEditor,
    createBackflowClient,
    createDocument,
    addNode,
    parseDocument,
    serializeDocument,
    type BackflowCatalogDescriptor,
    type BackflowCanvasNode,
} from "@shiron/backflow-react";
import type { ReactFlowInstance, Edge } from "@xyflow/react";
import "@xyflow/react/dist/style.css";

const client = createBackflowClient({ baseUrl: "/api/graphs" });
function App() {
    const [catalog, setCatalog] = useState<BackflowCatalogDescriptor>({
        nodes: [],
    });
    const [document, setDocument] = useState(createDocument);
    const [status, setStatus] = useState("Loading real ASP catalog…");
    const [selected, setSelected] = useState<string[]>([]);
    const instance = useRef<ReactFlowInstance<BackflowCanvasNode, Edge> | null>(
        null,
    );
    useEffect(() => {
        const controller = new AbortController();
        void client
            .catalog(controller.signal)
            .then((value) => {
                setCatalog(value);
                setStatus("Ready");
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setStatus(String(error));
            });
        try {
            const saved = localStorage.getItem("backflow-sample");
            if (saved) setDocument(parseDocument(saved));
        } catch (error) {
            setStatus(String(error));
        }
        return () => controller.abort();
    }, []);
    useEffect(() => {
        const controller = new AbortController();
        const handler = (event: KeyboardEvent) => {
            if (!event.ctrlKey && !event.metaKey) return;
            if (event.key.toLowerCase() === "s") {
                event.preventDefault();
                localStorage.setItem(
                    "backflow-sample",
                    serializeDocument(document),
                );
                setStatus("Saved in this browser");
            }
            if (event.key === "Enter") {
                event.preventDefault();
                void client
                    .validate(document.graph, controller.signal)
                    .then((result) => setStatus(JSON.stringify(result)))
                    .catch((error: unknown) => {
                        if (!controller.signal.aborted)
                            setStatus(String(error));
                    });
            }
            if (event.key.toLowerCase() === "b") {
                event.preventDefault();
                void client
                    .build(document.graph, controller.signal)
                    .then(() =>
                        setStatus(
                            "Topology compiled by ASP (not persisted or executed)",
                        ),
                    )
                    .catch((error: unknown) => {
                        if (!controller.signal.aborted)
                            setStatus(String(error));
                    });
            }
        };
        window.addEventListener("keydown", handler);
        return () => {
            controller.abort();
            window.removeEventListener("keydown", handler);
        };
    }, [document]);
    return (
        <main>
            <h1>Backflow React</h1>
            <p>
                Drag a published node onto the canvas. Connect declared ports.
                Delete removes selected nodes/edges. Ctrl/⌘ S saves locally;
                Ctrl/⌘ Enter validates; Ctrl/⌘ B compiles topology. No execution
                is enabled.
            </p>
            <ul aria-label="Published catalog">
                {catalog.nodes.map((entry) => (
                    <li
                        key={entry.id}
                        draggable
                        onDragStart={(event) =>
                            event.dataTransfer.setData(
                                "application/backflow-catalog-id",
                                entry.id,
                            )
                        }
                    >
                        {entry.id} (
                        {entry.isExecutable ? "executable" : "declaration only"}
                        )
                    </li>
                ))}
            </ul>
            <p role="status">{status}</p>
            <div
                style={{ height: 500 }}
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => {
                    event.preventDefault();
                    const catalogId = event.dataTransfer.getData(
                        "application/backflow-catalog-id",
                    );
                    const position = instance.current?.screenToFlowPosition({
                        x: event.clientX,
                        y: event.clientY,
                    });
                    if (catalogId && position)
                        setDocument((value) =>
                            addNode(
                                value,
                                catalog,
                                catalogId,
                                crypto.randomUUID(),
                                position,
                            ),
                        );
                }}
            >
                <BackflowEditor
                    document={document}
                    catalog={catalog}
                    onChange={setDocument}
                    selectedNodeIds={selected}
                    onSelectionChange={setSelected}
                    onInit={(value) => {
                        instance.current = value;
                    }}
                />
            </div>
            <details>
                <summary>Host-owned saved document</summary>
                <pre>{serializeDocument(document)}</pre>
            </details>
        </main>
    );
}
const root = document.getElementById("root");
if (!root) throw new Error("Root element is missing");
createRoot(root).render(<App />);
