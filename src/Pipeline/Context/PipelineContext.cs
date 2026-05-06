using System.Collections.Concurrent;
using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Thread-safe <see cref="IPipelineContext"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for parallel node execution.
/// </summary>
public class PipelineContext : IPipelineContext {
    private readonly ConcurrentBucketStore<Guid> _store = new();

    public void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value) {
        _store.Set(node.Mappings[port], value);
    }
    public void Write<T>(Guid id, T? value) {
        _store.Set(id, value);
    }
    public void Write(Guid id, object? value) {
        _store.Set(id, value);
    }
    public T? Read<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return _store.Get<T>(node.Mappings[port]);
    }
    public T? Read<T>(Guid id) {
        return _store.Get<T>(id);
    }
    public object? ReadAny(Guid id) {
        return _store.GetAny(id);
    }
    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return _store.Has<T>(node.Mappings[port]);
    }
    public bool Has<T>(Guid id) {
        return _store.Has<T>(id);
    }
    public bool HasAny(Guid id) {
        return _store.HasAny(id);
    }
}
