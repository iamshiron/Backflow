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

public record InputDto(
    object? Value,
    string Type
);

/// <summary>Serializable representation of a <see cref="PipelineBuilder.NodeInstance"/>.</summary>
public record NodeInstanceDto(
    string Id,
    string NodeTypeName,
    Dictionary<string, Guid> PortMappings
);

/// <summary>Serializable representation of a <see cref="PipelineBuilder.EdgeInstance"/>.</summary>
public record EdgeDto(
    string SourceNodeId,
    string SourcePortName,
    string DestinationNodeId,
    string DestinationPortName
);
