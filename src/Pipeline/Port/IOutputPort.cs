using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Typed output port. Writes values into the execution context for downstream consumption.
/// </summary>
public interface IOutputPort<in T> : IPort {
    /// <summary>Write a value to the context.</summary>
    void Write(INodeContext context, T value);
}
