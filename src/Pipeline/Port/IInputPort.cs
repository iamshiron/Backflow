using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Typed input port. Reads values from the execution context and applies fail-fast validation.
/// </summary>
public interface IInputPort<T> : IPort {
    /// <summary>Read the current value from the context, falling back to the port's default if unset.</summary>
    T? Read(INodeContext context);
    /// <summary>
    /// Read any value.
    /// </summary>
    /// <remarks>
    /// CAUTION: This causes boxing on value types. Only use when necessary. Preferably use <see cref="Read(INodeContext)"/> instead.
    /// </remarks>
    /// <summary>Read without generic type — causes boxing on value types.</summary>
    object? ReadAny(INodeContext context);

    /// <summary>Attempt to read the value; returns <c>false</c> if the port has never been written.</summary>
    bool TryRead(INodeContext context, out T? value);

    /// <summary>Whether a value has been written to this port in the current context.</summary>
    bool HasValue(INodeContext context);
}
