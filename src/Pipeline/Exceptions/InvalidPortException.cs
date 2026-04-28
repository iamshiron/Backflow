namespace Shiron.Lib.Pipeline.Exceptions;

public class InvalidPortException(Port port, Type expectedNodeType)
    : Exception($"Port '{port.Name}' does not belong to node of type '{expectedNodeType.Name}'.") {
    public Port Port { get; } = port;
    public Type ExpectedNodeType { get; } = expectedNodeType;
}
