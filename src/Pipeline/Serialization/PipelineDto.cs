namespace Shiron.Lib.Pipeline.Serialization;

/// <summary>Serializable representation of a <see cref="Pipeline"/> topology.</summary>
public record PipelineDto(
    NodeInstanceDto[] Nodes,
    EdgeDto[] Edges
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
