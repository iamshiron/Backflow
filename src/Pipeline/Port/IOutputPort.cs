using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public interface IOutputPort<in T> : IPort {
    void Write(INodeContext context, T value);
}
