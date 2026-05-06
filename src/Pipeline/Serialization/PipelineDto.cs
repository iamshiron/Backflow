using Shiron.Lib.Collections.Bucket;

namespace Shiron.Lib.Pipeline.Serialization;

/// <summary>Serializable representation of a <see cref="Pipeline"/> topology.</summary>
public record PipelineDto(
    NodeInstanceDto[] Nodes,
    EdgeDto[] Edges,
    IDictionary<Guid, InputDto> Inputs
);

public record InputDto(
    object? Value,
    string Type
);

/// <summary>Serializable representation of a <see cref="PipelineBuilder.NodeInstance"/>.</summary>
public record NodeInstanceDto(
    Guid Id,
    string NodeTypeName,
    Dictionary<string, Guid> PortMappings
);

/// <summary>Serializable representation of a <see cref="PipelineBuilder.EdgeInstance"/>.</summary>
public record EdgeDto(
    Guid SourceNodeId,
    string SourcePortName,
    Guid DestinationNodeId,
    string DestinationPortName
);
