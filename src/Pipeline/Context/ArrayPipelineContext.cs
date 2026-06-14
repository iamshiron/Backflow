using System.Collections.Concurrent;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

public class ArrayPipelineContext : IPipelineContext {
    public interface IPipelineContextBucket;
    public record PipelineContextBucket<T>(T?[] Data) : IPipelineContextBucket;

    private readonly CastRegistry _castRegistry;
    private readonly ConcurrentDictionary<Type, IPipelineContextBucket> _items = [];
    private readonly ConcurrentDictionary<Guid, int> _indices = [];
    private readonly ConcurrentDictionary<Guid, Type> _types = [];

    public ArrayPipelineContext(CastRegistry castRegistry, Dictionary<Type, int> sizes, Dictionary<Guid, int> indexMappings) {
        _castRegistry = castRegistry;
        foreach (var (type, size) in sizes) {
            CreateBucket(type, size);
        }
        foreach (var (id, index) in indexMappings) {
            _indices[id] = index;
        }
    }

    public void CreateBucket(Type t, int size) {
        var array = Array.CreateInstance(t, size);
        var bucket = Activator.CreateInstance(typeof(PipelineContextBucket<>).MakeGenericType(t), array);
        if (bucket is not IPipelineContextBucket b) throw new Exception("Could not create bucket");
        _items[t] = b;
    }
    public PipelineContextBucket<T>? GetBucket<T>() {
        return _items[typeof(T)] as PipelineContextBucket<T>;
    }

    public void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        var index = _indices[node.Mappings[port]];
        bucket.Data[index] = value;
    }
    public void Write<T>(Guid id, T? value) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        var index = _indices[id];
        bucket.Data[index] = value;
    }
    public void Write(Guid id, object? value) {
        var bucket = GetBucket<object>() ?? throw new InvalidOperationException("No bucket for type " + typeof(object));
        var index = _indices[id];
        bucket.Data[index] = value;
    }
    public T? Read<T>(PipelineBuilder.NodeInstance node, IPort port) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        var index = _indices[node.Mappings[port]];
        return bucket.Data[index];
    }
    public T? Read<T>(Guid id) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        var index = _indices[id];
        return bucket.Data[index];
    }
    public object? ReadAny(Guid id) {
        var bucket = GetBucket<object>() ?? throw new InvalidOperationException("No bucket for type " + typeof(object));
        var index = _indices[id];
        return bucket.Data[index];
    }
    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        var index = _indices[node.Mappings[port]];
        if (bucket.Data[index] is not null) return true;

        var storedType = _types.GetValueOrDefault(node.Mappings[port]);
        return storedType is not null && _castRegistry.CanCast(storedType, typeof(T));
    }
    public bool Has<T>(Guid id) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        var index = _indices[id];
        if (bucket.Data[index] is not null) return true;
        return _types.TryGetValue(id, out var storedType) && _castRegistry.CanCast(storedType, typeof(T));
    }
    public bool HasAny(Guid id) {
        return _types.ContainsKey(id);
    }
    public void WriteAt<T>(PipelineBuilder.NodeInstance node, IArrayInputPortMarker port, int index, T? value) {
        var bucket = GetBucket<T>() ?? throw new InvalidOperationException("No bucket for type " + typeof(T));
        bucket.Data[index] = value;
    }
}
