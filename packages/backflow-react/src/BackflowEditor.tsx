import { useMemo, useState, type ReactNode, type CSSProperties } from "react";
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
    type Viewport,
    type OnMove,
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
    portDescriptor,
} from "./document.js";
import { useNodeMeasurements } from "./useNodeMeasurements.js";

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
        context: BackflowNodeContext,
    ) => ReactNode;
    portStyle?: (
        port: BackflowPortDescriptor,
        index: number | null,
        context: BackflowNodeContext,
    ) => CSSProperties | undefined;
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
                        style={data.portStyle?.(port, index, {
                            id,
                            descriptor: data.descriptor,
                            selected,
                        })}
                    />
                    {data.renderPort?.(port, index, {
                        id,
                        descriptor: data.descriptor,
                        selected,
                    }) ?? (
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
    selectedEdgeIds?: readonly string[];
    onEdgeSelectionChange?: (ids: string[]) => void;
    onInit?: (instance: ReactFlowInstance<BackflowCanvasNode, Edge>) => void;
    renderNode?: CanvasData["renderNode"];
    renderPort?: CanvasData["renderPort"];
    /** CSS is applied only at the ReactFlow rendering boundary, not to domain values. */
    portStyle?: CanvasData["portStyle"];
    edgeStyle?: (
        edge: BackflowEdge,
        sourcePort: BackflowPortDescriptor | undefined,
    ) => CSSProperties | undefined;
    viewport?: Viewport;
    defaultViewport?: Viewport;
    onViewportChange?: (viewport: Viewport) => void;
    onMoveEnd?: OnMove;
    /** Defaults to false when viewport/defaultViewport is provided; otherwise true. */
    fitView?: boolean;
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
    selectedNodeIds,
    onSelectionChange,
    selectedEdgeIds,
    onEdgeSelectionChange,
    onInit,
    renderNode,
    renderPort,
    portStyle,
    edgeStyle,
    viewport,
    defaultViewport,
    onViewportChange,
    onMoveEnd,
    fitView,
    arrayIndices,
    children,
    readOnly = false,
}: BackflowEditorProps) {
    const [localNodes, setLocalNodes] = useState<string[]>([]);
    const [localEdges, setLocalEdges] = useState<string[]>([]);
    const nodeSelection = selectedNodeIds ?? localNodes;
    const edgeSelection = selectedEdgeIds ?? localEdges;
    const editable = !readOnly && onChange !== undefined;
    const nodeIds = useMemo(
        () => document.graph.nodes.map((node) => node.id),
        [document.graph.nodes],
    );
    const { measurements, onDimensionsChange } = useNodeMeasurements(nodeIds);
    const nodes = useMemo<BackflowCanvasNode[]>(
        () =>
            document.graph.nodes.map((node, order) => {
                const descriptor = catalog.nodes.find(
                    (entry) => entry.id === node.catalogId,
                );
                if (!descriptor)
                    throw new Error(
                        `Catalog entry ${node.catalogId} is not published; reload or migrate the document`,
                    );
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
                    measured: measurements.get(node.id),
                    type: "backflow",
                    position: document.positions[node.id] ?? {
                        x: order * 240,
                        y: 0,
                    },
                    selected: nodeSelection.includes(node.id),
                    data: {
                        descriptor,
                        slots,
                        renderNode,
                        renderPort,
                        portStyle,
                    },
                };
            }),
        [
            document,
            catalog,
            nodeSelection,
            renderNode,
            renderPort,
            portStyle,
            arrayIndices,
            measurements,
        ],
    );
    const edges = useMemo<Edge[]>(
        () =>
            document.graph.edges.map((edge) => ({
                id: edgeId(edge),
                selected: edgeSelection.includes(edgeId(edge)),
                style: edgeStyle?.(
                    edge,
                    portDescriptor(
                        document,
                        catalog,
                        edge.sourceNodeId,
                        edge.sourcePort,
                        BackflowPortDirection.Output,
                    ),
                ),
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
        [document, catalog, edgeSelection, edgeStyle],
    );
    return (
        <ReactFlow<BackflowCanvasNode, Edge>
            nodes={nodes}
            edges={edges}
            nodeTypes={nodeTypes}
            onInit={onInit}
            viewport={viewport}
            defaultViewport={defaultViewport}
            onViewportChange={onViewportChange}
            onMoveEnd={onMoveEnd}
            fitView={
                fitView ??
                (viewport === undefined && defaultViewport === undefined)
            }
            nodesDraggable={editable}
            nodesConnectable={editable}
            edgesReconnectable={false}
            deleteKeyCode={editable ? ["Backspace", "Delete"] : null}
            onNodesChange={(changes) => {
                onDimensionsChange(
                    changes.filter((change) => change.type === "dimensions"),
                );
                const selection = changes.filter(
                    (change) => change.type === "select",
                );
                if (selection.length) {
                    const ids = new Set(nodeSelection);
                    for (const change of selection) {
                        if (change.selected) ids.add(change.id);
                        else ids.delete(change.id);
                    }
                    const next = [...ids];
                    if (selectedNodeIds === undefined) setLocalNodes(next);
                    onSelectionChange?.(next);
                }
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
            onEdgesChange={(changes) => {
                const selection = changes.filter(
                    (change) => change.type === "select",
                );
                if (!selection.length) return;
                const ids = new Set(edgeSelection);
                for (const change of selection) {
                    if (change.selected) ids.add(change.id);
                    else ids.delete(change.id);
                }
                const next = [...ids];
                if (selectedEdgeIds === undefined) setLocalEdges(next);
                onEdgeSelectionChange?.(next);
            }}
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
