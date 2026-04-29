namespace Shiron.Lib.Pipeline.Exceptions;

/// <summary>Thrown when a port does not belong to the expected node type.</summary>
public class InvalidPortException(Port port, Type expectedNodeType)
    : Exception($"Port '{port.Name}' does not belong to node of type '{expectedNodeType.Name}'.") {
    /// <summary>The port that was passed.</summary>
    public Port Port { get; } = port;

    /// <summary>The node type that was expected to own the port.</summary>
    public Type ExpectedNodeType { get; } = expectedNodeType;
}
