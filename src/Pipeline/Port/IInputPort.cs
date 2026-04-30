using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public interface IInputPort<T> : IPort {
    T? Read(INodeContext context);
    bool TryRead(INodeContext context, out T? value);
    bool HasValue(INodeContext context);
}
