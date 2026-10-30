using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>Executable topology only; presentation and persistence belong to the host.</summary>
public sealed record BackflowGraph(BackflowNode[] Nodes, BackflowEdge[] Edges);

/// <summary>A node instance referencing an explicitly published catalog identifier.</summary>
public sealed record BackflowNode(string Id, string CatalogId);

/// <summary>A directed connection; an index targets one element of an array input.</summary>
public sealed record BackflowEdge(string SourceNodeId, string SourcePort, string DestinationNodeId,
    string DestinationPort, int? DestinationIndex = null);

/// <summary>Port direction derived from the registered node declaration.</summary>
public enum BackflowPortDirection {
    /// <summary>An input port.</summary>
    Input,
    /// <summary>An output port.</summary>
    Output
}

/// <summary>Port metadata and JSON schema derived from its actual CLR declaration.</summary>
public sealed record BackflowPortDescriptor(string Name, BackflowPortDirection Direction, string ClrType,
    JsonNode Schema, bool Required, int? MinimumCount, int? MaximumCount);

/// <summary>A host-published concrete node, including approved closed generic variants.</summary>
public sealed record BackflowNodeDescriptor(string Id, BackflowPortDescriptor[] Ports, bool IsExecutable,
    JsonNode? ConfigurationSchema = null);

/// <summary>Published node declarations; no client-supplied CLR names are resolved.</summary>
public sealed record BackflowCatalogDescriptor(BackflowNodeDescriptor[] Nodes);

/// <summary>Stable validation error categories.</summary>
public enum BackflowErrorCode {
    /// <summary>The request is not valid JSON or has missing fields.</summary>
    InvalidRequest,
    /// <summary>A configured resource bound was exceeded.</summary>
    LimitExceeded,
    /// <summary>A node was not published by the host.</summary>
    UnknownNode,
    /// <summary>The topology is invalid.</summary>
    InvalidGraph,
    /// <summary>A port, direction, type or connection is invalid.</summary>
    InvalidPort,
    /// <summary>An execution input is invalid.</summary>
    InvalidInput,
    /// <summary>The catalog entry is a declaration-only contract, not an executable implementation.</summary>
    NotExecutable
}

/// <summary>A bounded, typed diagnostic without internal exception details.</summary>
public sealed record BackflowError(BackflowErrorCode Code, string Message, string? NodeId = null, int? EdgeIndex = null);

/// <summary>The graph validation response.</summary>
public sealed record BackflowValidationResult(bool IsValid, BackflowError[] Errors);

/// <summary>A stateless graph creation response; it does not imply persistence.</summary>
public sealed record BackflowGraphResult(BackflowGraph Graph);

/// <summary>Validated topology with node IDs in topological order; no hashing, persistence or execution is performed.</summary>
public sealed record BackflowCompiledGraph(BackflowGraph Graph, string[] TopologicalNodeIds);

/// <summary>A literal for an unconnected input, decoded only as its declared port type.</summary>
public sealed record BackflowInput(string NodeId, string PortName, JsonElement Value);

/// <summary>A request to submit work to the host's scheduling boundary.</summary>
public sealed record BackflowExecutionSubmission(BackflowGraph Graph, BackflowInput[] Inputs);

/// <summary>A host-issued scheduling receipt; job status and results remain host-owned.</summary>
public sealed record BackflowExecutionAccepted(string JobId);
