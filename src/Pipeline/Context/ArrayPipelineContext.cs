using System.Collections.Concurrent;
using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

public class ArrayPipelineContext : IPipelineContext {
    private readonly CastRegistry _castRegistry;
    private readonly ArrayBucketStore _store;

    private readonly Dictionary<Guid, int> _indexMappings;

    public ArrayPipelineContext(CastRegistry castRegistry, Dictionary<Type, int> sizes, Dictionary<Guid, int> indexMappings) {
        _castRegistry = castRegistry;
        _store = new ArrayBucketStore(sizes);
        _indexMappings = indexMappings;
    }

    private int GetIndex(Guid id) {
        return _indexMappings[id];
    }
    private int GetIndex(PipelineBuilder.NodeInstance node, IPort port) {
        return GetIndex(node.Mappings[port]);
    }

    public void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value) {
        _store.Set(GetIndex(node, port), value);
    }
    public void Write<T>(Guid id, T? value) {
        _store.Set(GetIndex(id), value);
    }
    public void Write(Guid id, object? value) {
        _store.Set(GetIndex(id), value);
    }
    public T? Read<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return _store.Get<T>(GetIndex(node, port));
    }
    public T? Read<T>(Guid id) {
        return _store.Get<T>(GetIndex(id));
    }
    public object? ReadAny(Guid id) {
        return _store.GetAny(GetIndex(id));
    }
    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return _store.Has<T>(GetIndex(node, port));
    }
    public bool Has<T>(Guid id) {
        return _store.Has<T>(GetIndex(id));
    }
    public bool HasAny(Guid id) {
        return _store.HasAny(GetIndex(id));
    }
    public void WriteAt<T>(PipelineBuilder.NodeInstance node, IArrayInputPortMarker port, int index, T? value) {
        _store.Set(GetIndex(node, port) + index, value);
    }
}
