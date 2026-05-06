using System.Collections.Concurrent;
using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Thread-safe <see cref="IPipelineContext"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for parallel node execution.
/// </summary>
public class PipelineContext : IPipelineContext {
    internal readonly ConcurrentBucketStore<Guid> Store = new();

    public void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value) {
        Store.Set(node.Mappings[port], value);
    }
    public void Write<T>(Guid id, T? value) {
        Store.Set(id, value);
    }
    public void Write(Guid id, object? value) {
        Store.Set(id, value);
    }
    public T? Read<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return Store.Get<T>(node.Mappings[port]);
    }
    public T? Read<T>(Guid id) {
        return Store.Get<T>(id);
    }
    public object? ReadAny(Guid id) {
        return Store.GetAny(id);
    }
    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return Store.Has<T>(node.Mappings[port]);
    }
    public bool Has<T>(Guid id) {
        return Store.Has<T>(id);
    }
    public bool HasAny(Guid id) {
        return Store.HasAny(id);
    }
}
