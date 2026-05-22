using System.Collections.Concurrent;
using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Default <see cref="IPipelineContext"/> implementation backed by a <see cref="ConcurrentBucketStore{Guid}"/>.
/// Applies registered <see cref="CastRegistry"/> rules on read to support implicit type conversion.
/// </summary>
public class PipelineContext(CastRegistry castRegistry, IServiceProvider? serviceProvider = null) : IPipelineContext {
    internal readonly ConcurrentBucketStore<Guid> Store = new();
    public IServiceProvider Services { get; } = serviceProvider ?? EmptyServiceProvider.Instance;

    public PipelineContext(IServiceProvider? serviceProvider = null) : this(CastRegistry.Default, serviceProvider) {
    }

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
        return Read<T>(node.Mappings[port]);
    }
    public T? Read<T>(Guid id) {
        if (Store.Has<T>(id))
            return Store.Get<T>(id);

        if (Store.CanCast<T>(id))
            return Store.GetAs<T>(id);

        var storedType = Store.TypeOf(id);
        if (storedType is not null && castRegistry.TryGetCast(storedType, typeof(T), out var rule)) {
            var raw = Store.GetAny(id);
            return raw is not null ? (T?) rule!.Cast(raw) : default;
        }

        return default;
    }
    public object? ReadAny(Guid id) {
        return Store.GetAny(id);
    }
    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return Has<T>(node.Mappings[port]);
    }
    public bool Has<T>(Guid id) {
        if (Store.Has<T>(id) || Store.CanCast<T>(id)) return true;
        var storedType = Store.TypeOf(id);
        return storedType is not null && castRegistry.CanCast(storedType, typeof(T));
    }
    public bool HasAny(Guid id) {
        return Store.HasAny(id);
    }

    public void WriteAt<T>(PipelineBuilder.NodeInstance node, IArrayInputPortMarker port, int index, T? value) {
        var guid = node.Mappings[(IPort) port];
        var existing = Store.Get<T[]>(guid);
        T[] array;

        if (existing is not null) {
            array = existing;
        } else if (port.Count.HasValue) {
            array = new T[port.Count.Value];
        } else {
            array = new T[index + 1];
        }

        if (index >= array.Length) {
            var resized = new T[index + 1];
            Array.Copy(array, resized, array.Length);
            array = resized;
        }

        array[index] = value!;
        Store.Set(guid, array);
    }

    public sealed class EmptyServiceProvider : IServiceProvider {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) {
            return null;
        }
    }
}
