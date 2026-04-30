using System.Collections.Concurrent;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Thread-safe <see cref="IPipelineContext"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for parallel node execution.
/// </summary>
public class PipelineContext : IPipelineContext {
    private readonly ConcurrentDictionary<Guid, object> _memory = [];

    /// <inheritdoc/>
    public void Write<T>(PipelineBuilder.NodeInstance instance, IPort port, object value) {
        var connectionId = instance.Mappings[port];
        _memory[connectionId] = value;
    }
    /// <inheritdoc/>
    public object Read<T>(PipelineBuilder.NodeInstance node, IPort port) {
        var connectionId = node.Mappings[port];
        return _memory[connectionId];
    }
    /// <inheritdoc/>
    public void Write(Guid id, object value) {
        _memory[id] = value;
    }
    /// <inheritdoc/>
    public object Read(Guid id) {
        return _memory[id];
    }
}
