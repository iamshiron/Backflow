import { useMemo, type ReactNode } from "react";
import {
    ReactFlow,
    Background,
    Handle,
    Position,
    type Node,
    type NodeProps,
    type Edge,
    type Connection,
    type ReactFlowInstance,
} from "@xyflow/react";
import {
    BackflowPortDirection,
    type BackflowCatalogDescriptor,
    type BackflowNodeDescriptor,
    type BackflowPortDescriptor,
    type BackflowEdge,
} from "@shiron/backflow-client";
import {
    canConnect,
    connect,
    edgeId,
    removeNode,
    type BackflowDocument,
} from "./document.js";

export interface BackflowNodeContext {
    id: string;
    descriptor: BackflowNodeDescriptor;
    selected: boolean;
}
interface CanvasData extends Record<string, unknown> {
    descriptor: BackflowNodeDescriptor;
    slots: { port: BackflowPortDescriptor; index: number | null }[];
    renderNode?: (context: BackflowNodeContext) => ReactNode;
    renderPort?: (
        port: BackflowPortDescriptor,
        index: number | null,
    ) => ReactNode;
}
export type BackflowCanvasNode = Node<CanvasData, "backflow">;

function CatalogNode({ id, data, selected }: NodeProps<BackflowCanvasNode>) {
    return (
        <div
            style={{
                padding: 12,
                minWidth: 180,
                border: `1px solid ${selected ? "Highlight" : "GrayText"}`,
                borderRadius: 6,
                background: "Canvas",
                color: "CanvasText",
            }}
        >
            {data.renderNode?.({
                id,
                descriptor: data.descriptor,
                selected,
            }) ?? <strong>{data.descriptor.id}</strong>}
            {data.slots.map(({ port, index }) => (
                <div
                    key={handleId(port.name, index, port.direction)}
                    style={{
                        position: "relative",
                        padding: "6px 8px",
                        textAlign:
                            port.direction === BackflowPortDirection.Input
                                ? "left"
                                : "right",
                    }}
                >
                    <Handle
                        id={handleId(port.name, index, port.direction)}
                        type={
                            port.direction === BackflowPortDirection.Input
                                ? "target"
                                : "source"
                        }
                        position={
                            port.direction === BackflowPortDirection.Input
                                ? Position.Left
                                : Position.Right
                        }
                        aria-label={`${port.name}${index === null ? "" : `[${index}]`} ${port.direction === BackflowPortDirection.Input ? "input" : "output"}`}
                    />
                    {data.renderPort?.(port, index) ?? (
                        <span title={port.clrType}>
                            {port.name}
                            {index === null ? "" : `[${index}]`}
                            {port.required ? " *" : ""}
                        </span>
                    )}
                </div>
            ))}
        </div>
    );
}
const nodeTypes = { backflow: CatalogNode };
/** Handle identities are presentation-only, and encode names without separator ambiguity. */
export function handleId(
    name: string,
    index: number | null,
    direction: BackflowPortDirection,
): string {
    return JSON.stringify([name, index, direction]);
}
function readHandle(
    value: string | null | undefined,
): { name: string; index: number | null } | null {
    if (!value) return null;
    try {
        const parsed: unknown = JSON.parse(value);
        if (
            !Array.isArray(parsed) ||
            typeof parsed[0] !== "string" ||
            !(parsed[1] === null || typeof parsed[1] === "number")
        )
            return null;
        return { name: parsed[0], index: parsed[1] as number | null };
    } catch {
        return null;
    }
}
function toEdge(connection: Connection | Edge): BackflowEdge | null {
    const source = readHandle(connection.sourceHandle);
    const target = readHandle(connection.targetHandle);
    return source && target
        ? {
              sourceNodeId: connection.source,
              sourcePort: source.name,
              destinationNodeId: connection.target,
              destinationPort: target.name,
              destinationIndex: target.index,
          }
        : null;
}
export interface BackflowEditorProps {
    document: BackflowDocument;
    catalog: BackflowCatalogDescriptor;
    onChange?: (document: BackflowDocument) => void;
    selectedNodeIds?: readonly string[];
    onSelectionChange?: (ids: string[]) => void;
    onInit?: (instance: ReactFlowInstance<BackflowCanvasNode, Edge>) => void;
    renderNode?: CanvasData["renderNode"];
    renderPort?: CanvasData["renderPort"];
    /** Add indexed handles to the existing connected slots and next available slot. */
    arrayIndices?: (
        nodeId: string,
        port: BackflowPortDescriptor,
    ) => readonly number[];
    children?: ReactNode;
    readOnly?: boolean;
}
/** Controlled graph canvas. The host supplies a sized container, toolbar, persistence and auth. */
export function BackflowEditor({
    document,
    catalog,
    onChange,
    selectedNodeIds = [],
    onSelectionChange,
    onInit,
    renderNode,
    renderPort,
    arrayIndices,
    children,
    readOnly = false,
}: BackflowEditorProps) {
    const editable = !readOnly && onChange !== undefined;
    const nodes = useMemo<BackflowCanvasNode[]>(
        () =>
            document.graph.nodes.map((node, order) => {
                const descriptor = catalog.nodes.find(
                    (entry) => entry.id === node.catalogId,
                ) ?? {
                    id: node.catalogId,
                    ports: [],
                    isExecutable: false,
                    configurationSchema: null,
                };
                const slots = descriptor.ports.flatMap((port) => {
                    const result = [{ port, index: null as number | null }];
                    if (
                        port.direction !== BackflowPortDirection.Input ||
                        port.minimumCount === null
                    )
                        return result;
                    const indices = document.graph.edges
                        .filter(
                            (edge) =>
                                edge.destinationNodeId === node.id &&
                                edge.destinationPort === port.name &&
                                edge.destinationIndex !== null,
                        )
                        .map((edge) => edge.destinationIndex as number);
                    const next = indices.length ? Math.max(...indices) + 1 : 0;
                    const all = [
                        ...indices,
                        ...(arrayIndices?.(node.id, port) ?? []),
                        next,
                    ];
                    return [
                        ...result,
                        ...[...new Set(all)]
                            .filter(
                                (index) =>
                                    Number.isInteger(index) &&
                                    index >= 0 &&
                                    (port.maximumCount === null ||
                                        index < port.maximumCount),
                            )
                            .sort((a, b) => a - b)
                            .map((index) => ({ port, index })),
                    ];
                });
                return {
                    id: node.id,
                    type: "backflow",
                    position: document.positions[node.id] ?? {
                        x: order * 240,
                        y: 0,
                    },
                    selected: selectedNodeIds.includes(node.id),
                    data: { descriptor, slots, renderNode, renderPort },
                };
            }),
        [
            document,
            catalog,
            selectedNodeIds,
            renderNode,
            renderPort,
            arrayIndices,
        ],
    );
    const edges = useMemo<Edge[]>(
        () =>
            document.graph.edges.map((edge) => ({
                id: edgeId(edge),
                source: edge.sourceNodeId,
                sourceHandle: handleId(
                    edge.sourcePort,
                    null,
                    BackflowPortDirection.Output,
                ),
                target: edge.destinationNodeId,
                targetHandle: handleId(
                    edge.destinationPort,
                    edge.destinationIndex,
                    BackflowPortDirection.Input,
                ),
            })),
        [document.graph.edges],
    );
    return (
        <ReactFlow<BackflowCanvasNode, Edge>
            nodes={nodes}
            edges={edges}
            nodeTypes={nodeTypes}
            onInit={onInit}
            fitView
            nodesDraggable={editable}
            nodesConnectable={editable}
            edgesReconnectable={false}
            deleteKeyCode={editable ? ["Backspace", "Delete"] : null}
            onNodesChange={(changes) => {
                if (!editable) return;
                let next = document;
                for (const change of changes) {
                    if (change.type === "position" && change.position)
                        next = {
                            ...next,
                            positions: {
                                ...next.positions,
                                [change.id]: change.position,
                            },
                        };
                }
                if (next !== document) onChange?.(next);
            }}
            onEdgesChange={() => {}}
            onDelete={({ nodes: removedNodes, edges: removedEdges }) => {
                if (!editable) return;
                const next = removedNodes.reduce(
                    (value, node) => removeNode(value, node.id),
                    document,
                );
                const ids = new Set(removedEdges.map((edge) => edge.id));
                onChange?.({
                    ...next,
                    graph: {
                        ...next.graph,
                        edges: next.graph.edges.filter(
                            (edge) => !ids.has(edgeId(edge)),
                        ),
                    },
                });
            }}
            onSelectionChange={({ nodes: selected }) =>
                onSelectionChange?.(selected.map((node) => node.id))
            }
            isValidConnection={(connection) => {
                const edge = toEdge(connection);
                return (
                    editable &&
                    edge !== null &&
                    canConnect(document, catalog, edge)
                );
            }}
            onConnect={(connection) => {
                const edge = toEdge(connection);
                if (editable && edge && canConnect(document, catalog, edge))
                    onChange?.(connect(document, catalog, edge));
            }}
        >
            <Background />
            {children}
        </ReactFlow>
    );
}
