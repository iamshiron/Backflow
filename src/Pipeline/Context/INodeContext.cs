using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

public interface INodeContext {
    void Write<T>(IPort port, T? value);
    T? Read<T>(IPort port);
    void Write(IPort port, object? value);
    object? ReadAny(IPort port);
    bool Has<T>(IPort port);
    bool HasAny(IPort port);

    void InitializeArray<T>(IArrayInputPort<T> port, int count);
}
