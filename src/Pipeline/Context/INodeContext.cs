namespace Shiron.Lib.Pipeline.Context;

public interface INodeContext {
    void Write(Port port, object value);
    object? Read(Port port);
}
