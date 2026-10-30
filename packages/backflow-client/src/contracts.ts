// Generated from Backflow.Ext.AspNetCore. Run pnpm generate; do not edit.
export type JsonValue =
    | null
    | boolean
    | number
    | string
    | JsonValue[]
    | { [key: string]: JsonValue };
export interface BackflowCatalogDescriptor {
    nodes: BackflowNodeDescriptor[];
}
export interface BackflowCompiledGraph {
    graph: BackflowGraph;
    topologicalNodeIds: string[];
}
export interface BackflowEdge {
    sourceNodeId: string;
    sourcePort: string;
    destinationNodeId: string;
    destinationPort: string;
    destinationIndex: number | null;
}
export interface BackflowError {
    code: BackflowErrorCode;
    message: string;
    nodeId: string | null;
    edgeIndex: number | null;
}
export enum BackflowErrorCode {
    InvalidRequest = 0,
    LimitExceeded = 1,
    UnknownNode = 2,
    InvalidGraph = 3,
    InvalidPort = 4,
    InvalidInput = 5,
    NotExecutable = 6,
}
export interface BackflowExecutionAccepted {
    jobId: string;
}
export interface BackflowExecutionSubmission {
    graph: BackflowGraph;
    inputs: BackflowInput[];
}
export interface BackflowGraph {
    nodes: BackflowNode[];
    edges: BackflowEdge[];
}
export interface BackflowGraphResult {
    graph: BackflowGraph;
}
export interface BackflowInput {
    nodeId: string;
    portName: string;
    value: JsonValue;
}
export interface BackflowNode {
    id: string;
    catalogId: string;
}
export interface BackflowNodeDescriptor {
    id: string;
    ports: BackflowPortDescriptor[];
    isExecutable: boolean;
    configurationSchema: JsonValue | null;
}
export interface BackflowPortDescriptor {
    name: string;
    direction: BackflowPortDirection;
    clrType: string;
    schema: JsonValue;
    required: boolean;
    minimumCount: number | null;
    maximumCount: number | null;
}
export enum BackflowPortDirection {
    Input = 0,
    Output = 1,
}
export interface BackflowValidationResult {
    isValid: boolean;
    errors: BackflowError[];
}
