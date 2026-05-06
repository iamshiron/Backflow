using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public interface IInputPort<T> : IPort {
    T? Read(INodeContext context);
    /// <summary>
    /// Read any value.
    /// </summary>
    /// <remarks>
    /// CAUTION: This causes boxing on value types. Only use when necessary. Preferably use <see cref="Read(INodeContext)"/> instead.
    /// </remarks>
    object? ReadAny(INodeContext context);

    bool TryRead(INodeContext context, out T? value);
    bool HasValue(INodeContext context);
}
