using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

public interface INodeContext {
    void Write<T>(IPort port, T? value);
    T? Read<T>(IPort port);
    void Write(IPort port, object? value);
    object? ReadAny(IPort port);
    bool Has<T>(IPort port);
    bool HasAny(IPort port);

    T? ReadGroup<T>(IPort group, int index);
    void WriteGroup<T>(IPort group, int index, T? value);
    bool HasGroup<T>(IPort group, int index);
    int GetGroupCount(IPort group);
}
