namespace Shiron.Lib.Pipeline.Generic;

/// <summary>Describes a port's type metadata on an open generic node blueprint.</summary>
/// <param name="Name">Port property name.</param>
/// <param name="Direction">Input or output.</param>
/// <param name="TypeParameterIndex">If the port's type is a generic parameter, its 0-based position; otherwise <c>null</c>.</param>
/// <param name="ConcreteType">If the port has a concrete type, that type; otherwise <c>null</c>.</param>
public sealed record PortTypeMetadata(
    string Name,
    PortDirection Direction,
    int? TypeParameterIndex,
    Type? ConcreteType
);
