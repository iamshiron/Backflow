import {
    BackflowPortDirection,
    type BackflowCatalogDescriptor,
    type BackflowEdge,
    type BackflowGraph,
    type BackflowInput,
    type JsonValue,
} from "@shiron/backflow-client";

/** Editor-only data: never sent to the adapter topology endpoints. */
export interface BackflowDocument {
    graph: BackflowGraph;
    inputs: BackflowInput[];
    positions: Record<string, { x: number; y: number }>;
    /** Host-owned configuration values, described by each catalog configurationSchema. */
    configuration: Record<string, JsonValue>;
}
export const createDocument = (): BackflowDocument => ({
    graph: { nodes: [], edges: [] },
    inputs: [],
    positions: {},
    configuration: {},
});
export function addNode(
    document: BackflowDocument,
    catalog: BackflowCatalogDescriptor,
    catalogId: string,
    id: string,
    position = { x: 0, y: 0 },
): BackflowDocument {
    if (!catalog.nodes.some((node) => node.id === catalogId))
        throw new Error("Unknown catalog entry");
    if (document.graph.nodes.some((node) => node.id === id))
        throw new Error("Duplicate node ID");
    return {
        ...document,
        graph: {
            ...document.graph,
            nodes: [...document.graph.nodes, { id, catalogId }],
        },
        positions: { ...document.positions, [id]: position },
    };
}
export function removeNode(
    document: BackflowDocument,
    id: string,
): BackflowDocument {
    const positions = { ...document.positions };
    delete positions[id];
    const configuration = { ...document.configuration };
    delete configuration[id];
    return {
        ...document,
        positions,
        configuration,
        inputs: document.inputs.filter((input) => input.nodeId !== id),
        graph: {
            nodes: document.graph.nodes.filter((node) => node.id !== id),
            edges: document.graph.edges.filter(
                (edge) =>
                    edge.sourceNodeId !== id && edge.destinationNodeId !== id,
            ),
        },
    };
}
export function edgeId(edge: BackflowEdge): string {
    return JSON.stringify([
        edge.sourceNodeId,
        edge.sourcePort,
        edge.destinationNodeId,
        edge.destinationPort,
        edge.destinationIndex,
    ]);
}
export function portDescriptor(
    document: BackflowDocument,
    catalog: BackflowCatalogDescriptor,
    nodeId: string,
    portName: string,
    direction: BackflowPortDirection,
) {
    const node = document.graph.nodes.find((node) => node.id === nodeId);
    return catalog.nodes
        .find((entry) => entry.id === node?.catalogId)
        ?.ports.find(
            (port) => port.name === portName && port.direction === direction,
        );
}
/** Optimistic UX checks only. ASP validation remains authoritative. */
export function canConnect(
    document: BackflowDocument,
    catalog: BackflowCatalogDescriptor,
    edge: BackflowEdge,
): boolean {
    const source = portDescriptor(
        document,
        catalog,
        edge.sourceNodeId,
        edge.sourcePort,
        BackflowPortDirection.Output,
    );
    const target = portDescriptor(
        document,
        catalog,
        edge.destinationNodeId,
        edge.destinationPort,
        BackflowPortDirection.Input,
    );
    if (!source || !target || edge.sourceNodeId === edge.destinationNodeId)
        return false;
    const array = target.minimumCount !== null || target.maximumCount !== null;
    if (edge.destinationIndex !== null && !array) return false;
    const targetType =
        edge.destinationIndex === null
            ? target.clrType
            : target.clrType.replace(/\[\]$/, "");
    if (source.clrType !== targetType) return false;
    if (
        edge.destinationIndex !== null &&
        (!Number.isInteger(edge.destinationIndex) ||
            edge.destinationIndex < 0 ||
            (target.maximumCount !== null &&
                edge.destinationIndex >= target.maximumCount))
    )
        return false;
    if (
        document.inputs.some(
            (input) =>
                input.nodeId === edge.destinationNodeId &&
                input.portName === edge.destinationPort,
        )
    )
        return false;
    if (
        document.graph.edges.some(
            (item) =>
                item.destinationNodeId === edge.destinationNodeId &&
                item.destinationPort === edge.destinationPort &&
                (item.destinationIndex === edge.destinationIndex ||
                    item.destinationIndex === null ||
                    edge.destinationIndex === null),
        )
    )
        return false;
    const pending = [edge.destinationNodeId];
    const visited = new Set<string>();
    while (pending.length) {
        const id = pending.pop();
        if (id === edge.sourceNodeId) return false;
        if (id === undefined || visited.has(id)) continue;
        visited.add(id);
        pending.push(
            ...document.graph.edges
                .filter((item) => item.sourceNodeId === id)
                .map((item) => item.destinationNodeId),
        );
    }
    return true;
}
export function connect(
    document: BackflowDocument,
    catalog: BackflowCatalogDescriptor,
    edge: BackflowEdge,
): BackflowDocument {
    if (!canConnect(document, catalog, edge))
        throw new Error("Connection violates catalog or DAG constraints");
    return {
        ...document,
        graph: { ...document.graph, edges: [...document.graph.edges, edge] },
    };
}
/** Literal shape is described by port.schema; decoding and validation happen on the host. */
export function setLiteral(
    document: BackflowDocument,
    catalog: BackflowCatalogDescriptor,
    input: BackflowInput,
): BackflowDocument {
    if (
        !portDescriptor(
            document,
            catalog,
            input.nodeId,
            input.portName,
            BackflowPortDirection.Input,
        )
    )
        throw new Error("Unknown input");
    if (
        document.graph.edges.some(
            (edge) =>
                edge.destinationNodeId === input.nodeId &&
                edge.destinationPort === input.portName,
        )
    )
        throw new Error("Input already connected");
    return {
        ...document,
        inputs: [
            ...document.inputs.filter(
                (item) =>
                    item.nodeId !== input.nodeId ||
                    item.portName !== input.portName,
            ),
            input,
        ],
    };
}
