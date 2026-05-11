using System.Collections.Concurrent;
using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

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
        return Store.Get<T>(node.Mappings[port]) ?? Store.GetAs<T>(node.Mappings[port]) ?? default;
    }
    public T? Read<T>(Guid id) {
        return Store.Get<T>(id) ?? Store.GetAs<T>(id) ?? default;
    }
    public object? ReadAny(Guid id) {
        return Store.GetAny(id);
    }
    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return Store.Has<T>(node.Mappings[port]) || Store.CanCast<T>(node.Mappings[port]);
    }
    public bool Has<T>(Guid id) {
        return Store.Has<T>(id) || Store.CanCast<T>(id);
    }
    public bool HasAny(Guid id) {
        return Store.HasAny(id);
    }

    public void WriteGroup<T>(PipelineBuilder.NodeInstance node, IPort group, int index, T? value) {
        Store.Set(node.GroupMappings[group][index], value);
    }
    public T? ReadGroup<T>(PipelineBuilder.NodeInstance node, IPort group, int index) {
        var guid = node.GroupMappings[group][index];
        return Store.Get<T>(guid) ?? Store.GetAs<T>(guid) ?? default;
    }
    public bool HasGroup<T>(PipelineBuilder.NodeInstance node, IPort group, int index) {
        var guid = node.GroupMappings[group][index];
        return Store.Has<T>(guid) || Store.CanCast<T>(guid);
    }
}
