namespace Shiron.Lib.Pipeline.Serialization;

public record PipelineDto(
    NodeInstanceDto[] Nodes,
    EdgeDto[] Edges
);

public record NodeInstanceDto(
    Guid Id,
    string NodeTypeName,
    Dictionary<string, Guid> PortMappings
);

public record EdgeDto(
    Guid SourceNodeId,
    string SourcePortName,
    Guid DestinationNodeId,
    string DestinationPortName
);
