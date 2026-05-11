using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public interface IInputPortGroup<T> : IPortGroup {
    T? Read(INodeContext context, int index);
    IReadOnlyList<T?> ReadAll(INodeContext context);
    bool HasValue(INodeContext context, int index);
}
