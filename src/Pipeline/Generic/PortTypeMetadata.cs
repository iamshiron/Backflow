namespace Shiron.Lib.Pipeline.Generic;

public sealed record PortTypeMetadata(
    string Name,
    PortDirection Direction,
    int? TypeParameterIndex,
    Type? ConcreteType
);
