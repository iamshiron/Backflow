namespace Shiron.Lib.Pipeline.Serialization;

/// <summary>Serializable representation of a <see cref="Pipeline"/> topology (nodes + edges).</summary>
public record PipelineDefinitionDto(
    NodeInstanceDto[] Nodes,
    EdgeDto[] Edges
);

/// <summary>Serializable representation of pipeline inputs, keyed by node ID then port name.</summary>
public record PipelineInputsDto(
    IDictionary<string, Dictionary<string, InputDto>> Inputs
);

/// <summary>Serializable representation of an input port value with its runtime type.</summary>
/// <param name="SuppliedMask">For array inputs, packed bitmask of directly written indices (32 bits per int). <c>null</c> for non-array ports.</param>
public record InputDto(
    object? Value,
    string Type,
    int[]? SuppliedMask = null
);

/// <summary>Serializable representation of a <see cref="PipelineBuilder.NodeInstance"/>.</summary>
/// <remarks>
/// Port-to-channel mappings are intentionally omitted: channels are internal bus IDs and are
/// fully reconstructed from <see cref="EdgeDto"/> connectivity during deserialization.
/// </remarks>
public record NodeInstanceDto(
    string Id,
    string NodeTypeName,
    string[]? GenericTypeArgs = null
);

/// <summary>Serializable representation of a <see cref="PipelineBuilder.EdgeInstance"/>.</summary>
public record EdgeDto(
    string SourceNodeId,
    string SourcePortName,
    string DestinationNodeId,
    string DestinationPortName,
    int? DestIndex = null
);
