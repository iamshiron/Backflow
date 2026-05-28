using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Global shared memory for a pipeline execution. Passed to <see cref="PipelineExecutor.Execute"/>
/// and <see cref="PipelineExecutor.ExecuteAsync"/>.
/// </summary>
public interface IPipelineContext {
    /// <summary>Write via node + port (resolves to the shared channel GUID).</summary>
    /// <param name="node">Node instance that owns the port.</param>
    /// <param name="port">Target port on the node.</param>
    /// <param name="value">Value to write.</param>
    void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value);

    /// <summary>
    /// Write directly by channel GUID.
    /// </summary>
    void Write<T>(Guid id, T? value);

    /// <summary>Write directly by channel GUID.</summary>
    /// <param name="id">Channel GUID.</param>
    /// <param name="value">Value to write.</param>
    void Write(Guid id, object? value);

    /// <summary>Read via node + port (resolves to the shared channel GUID).</summary>
    /// <param name="node">Node instance that owns the port.</param>
    /// <param name="port">Source port on the node.</param>
    T? Read<T>(PipelineBuilder.NodeInstance node, IPort port);

    /// <summary>
    /// Read directly by channel GUID.
    /// </summary>
    T? Read<T>(Guid id);

    /// <summary>Read directly by channel GUID.</summary>
    /// <param name="id">Channel GUID.</param>
    object? ReadAny(Guid id);

    /// <summary>
    /// Returns <c>true</c> if the port is bound to a value.
    /// </summary>
    bool Has<T>(PipelineBuilder.NodeInstance node, IPort port);

    /// <summary>
    /// Returns <c>true</c> if the port is bound to a value.
    /// </summary>
    bool Has<T>(Guid id);

    /// <summary>
    /// Returns <c>true</c> if the port is stored in any bucket.
    /// </summary>
    bool HasAny(Guid id);

    /// <summary>
    /// Write to a specific index of an array input port. Reads the existing array (or creates a default one),
    /// sets the element at <paramref name="index"/> to <paramref name="value"/>, and writes it back.
    /// </summary>
    void WriteAt<T>(PipelineBuilder.NodeInstance node, IArrayInputPortMarker port, int index, T? value);
}
