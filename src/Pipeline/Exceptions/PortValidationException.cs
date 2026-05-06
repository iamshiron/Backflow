using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Exceptions;

public class PortValidationException(string portName, object? value, string error)
    : Exception($"Port '{portName}' failed validation: {error} (value: {value})") {
    public string PortName { get; } = portName;
    public object? Value { get; } = value;
    public string Error { get; } = error;
}
