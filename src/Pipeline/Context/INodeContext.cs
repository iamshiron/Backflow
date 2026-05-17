using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Per-node execution context. Provides typed read/write access to port values
/// via the node's port-to-GUID mapping. Passed to <see cref="AbstractNode.ExecuteNodeAsync"/>.
/// </summary>
public interface INodeContext {
    /// <summary>Write a typed value to a port.</summary>
    void Write<T>(IPort port, T? value);
    /// <summary>Read a typed value from a port.</summary>
    T? Read<T>(IPort port);
    /// <summary>Write an untyped value to a port.</summary>
    void Write(IPort port, object? value);
    /// <summary>Read an untyped value from a port.</summary>
    object? ReadAny(IPort port);
    /// <summary>Whether a typed value exists for the port.</summary>
    bool Has<T>(IPort port);
    /// <summary>Whether any value exists for the port.</summary>
    bool HasAny(IPort port);

    /// <summary>Initialize an array input port from indexed connections with an explicit count.</summary>
    void InitializeArray<T>(IArrayInputPort<T> port, int count);
}
