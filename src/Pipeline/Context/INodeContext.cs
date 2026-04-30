namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Per-node context used inside <see cref="AbstractNode.Execute"/>.
/// Translates port references to shared data channels via the port-to-GUID mapping.
/// </summary>
public interface INodeContext {
    /// <summary>Write a value to the channel backing <paramref name="port"/>.</summary>
    /// <param name="port">Target port.</param>
    /// <param name="value">Value to write.</param>
    void Write(Port.Port port, object value);

    /// <summary>Read the current value from the channel backing <paramref name="port"/>.</summary>
    /// <param name="port">Source port.</param>
    object? Read(Port.Port port);
}
