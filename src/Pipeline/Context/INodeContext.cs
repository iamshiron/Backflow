using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Per-node context used inside <see cref="AbstractNode.Execute"/>.
/// Translates port references to shared data channels via the port-to-GUID mapping.
/// </summary>
public interface INodeContext {
    /// <summary>Write a value to the channel backing <paramref name="port"/>.</summary>
    /// <param name="port">Target port.</param>
    /// <param name="value">Value to write.</param>
    void Write<T>(IPort port, T? value);

    /// <summary>Read the current value from the channel backing <paramref name="port"/>.</summary>
    /// <param name="port">Source port.</param>
    T? Read<T>(IPort port);

    /// <summary>
    /// Write a value to the channel backing <paramref name="port"/>.
    /// </summary>
    void Write(IPort port, object? value);
    /// <summary>
    /// Read the current value from the channel backing <paramref name="port"/>.
    /// </summary>
    object? ReadAny(IPort port);

    /// <summary>
    /// Returns <c>true</c> if the port is bound to a value.
    /// </summary>
    bool Has<T>(IPort port);

    /// <summary>
    /// Returns <c>true</c> if the port is stored in any bucket.
    /// </summary>
    bool HasAny(IPort port);
}
