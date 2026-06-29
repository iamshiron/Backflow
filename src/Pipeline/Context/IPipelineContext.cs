using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Global shared memory for a pipeline execution. Passed to <see cref="PipelineExecutor.Execute"/>
/// and <see cref="PipelineExecutor.ExecuteAsync"/>.
/// </summary>
public interface IPipelineContext {
    /// <summary>Write via node + port (resolves to the shared channel).</summary>
    void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value);

    /// <summary>Write directly by channel ID.</summary>
    void Write<T>(int channel, T? value);

    /// <summary>Write an untyped value by channel ID, using the channel's declared type for routing.</summary>
    void Write(int channel, object? value);

    /// <summary>Write an explicitly typed value by channel ID (used by serialization and cache restore).</summary>
    void Write(int channel, object? value, Type type);

    /// <summary>Read via node + port (resolves to the shared channel).</summary>
    T? Read<T>(PipelineBuilder.NodeInstance node, IPort port);

    /// <summary>Read directly by channel ID. Applies cast-on-read if the declared type differs from T.</summary>
    T? Read<T>(int channel);

    /// <summary>Read the boxed value by channel ID.</summary>
    object? ReadAny(int channel);

    /// <summary>Returns the declared type of the channel, or <c>null</c> if unknown.</summary>
    Type? TypeOf(int channel);

    /// <summary>Returns <c>true</c> if the channel is bound to a value readable as T.</summary>
    bool Has<T>(PipelineBuilder.NodeInstance node, IPort port);

    /// <summary>Returns <c>true</c> if the channel is bound to a value readable as T.</summary>
    bool Has<T>(int channel);

    /// <summary>Returns <c>true</c> if any value was written to the channel.</summary>
    bool HasAny(int channel);

    /// <summary>
    /// Write to a specific index of an array input port. Reads the existing array (or creates a default one),
    /// sets the element at <paramref name="index"/> to <paramref name="value"/>, and writes it back.
    /// </summary>
    void WriteAt<T>(PipelineBuilder.NodeInstance node, IArrayInputPortMarker port, int index, T? value);

    /// <summary>
    /// Returns a bitmask indicating which array indices were directly written via <see cref="WriteAt{T}"/>,
    /// packed as <c>int[]</c> (32 bits per element), or <c>null</c> if the channel received no indexed writes.
    /// </summary>
    int[]? GetSuppliedMask(int channel);

    /// <summary>
    /// Restore a supplied bitmask for a channel (used during deserialization).
    /// </summary>
    void SetSuppliedMask(int channel, int[]? mask);
}
