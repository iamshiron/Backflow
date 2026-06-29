using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Array-backed <see cref="IPipelineContext"/>. Channel IDs are sequential integers assigned during
/// <see cref="PipelineBuilder.Build"/> and used directly as keys into the underlying <see cref="ArrayBucketStore"/>.
/// No GUID-to-index translation is needed.
/// </summary>
public sealed class ArrayPipelineContext(
    CastRegistry castRegistry,
    ArrayBucketStore store,
    Type[] channelTypes
) : IPipelineContext {
    private readonly CastRegistry _castRegistry = castRegistry;
    private readonly ArrayBucketStore _store = store;
    private readonly Type[] _channelTypes = channelTypes;
    private readonly Dictionary<int, int[]> _writeAtMasks = [];

    /// <summary>Create a context for a built pipeline, computing the channel layout from its topology.</summary>
    public static ArrayPipelineContext ForPipeline(Pipeline pipeline, CastRegistry? castRegistry = null) {
        var registry = castRegistry ?? pipeline.CastRegistry;
        var (channelTypes, sizes) = PipelineBuilder.ComputeLayout(pipeline);
        var store = new ArrayBucketStore(sizes);
        return new ArrayPipelineContext(registry, store, channelTypes);
    }

    /// <summary>Create an ad-hoc context with explicitly declared channels (for tests or standalone use).</summary>
    public static ArrayPipelineContext Create(params Type[] channelTypes) {
        return Create(null, channelTypes);
    }

    /// <summary>Create an ad-hoc context with explicitly declared channels and a custom cast registry.</summary>
    public static ArrayPipelineContext Create(CastRegistry? castRegistry, params Type[] channelTypes) {
        var registry = castRegistry ?? CastRegistry.CreateDefault();
        var totalChannels = channelTypes.Length;
        var sizes = new Dictionary<Type, int>();
        foreach (var t in channelTypes) {
            var bucketType = t.IsValueType ? t : typeof(object);
            if (!sizes.ContainsKey(bucketType))
                sizes[bucketType] = totalChannels;
        }
        var store = new ArrayBucketStore(sizes);
        return new ArrayPipelineContext(registry, store, channelTypes);
    }

    private Type BucketType(int channel) {
        var declared = _channelTypes[channel];
        return declared.IsValueType ? declared : typeof(object);
    }

    public void Write<T>(PipelineBuilder.NodeInstance node, IPort port, T? value) {
        _store.Set(node.Mappings[port], value);
    }

    public void Write<T>(int channel, T? value) {
        _store.Set(channel, value);
    }

    public void Write(int channel, object? value) {
        _store.Set(channel, value, BucketType(channel));
    }

    public void Write(int channel, object? value, Type type) {
        _store.Set(channel, value, type);
    }

    public T? Read<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return Read<T>(node.Mappings[port]);
    }

    public T? Read<T>(int channel) {
        if (!_store.HasAny(channel)) return default;

        var declaredType = _channelTypes[channel];

        if (declaredType == typeof(T))
            return _store.Get<T>(channel);

        if (declaredType.IsAssignableTo(typeof(T)))
            return (T?) _store.GetAny(channel, BucketType(channel));

        if (_castRegistry.TryGetCast(declaredType, typeof(T), out var rule)) {
            var raw = _store.GetAny(channel, BucketType(channel));
            return raw is not null ? (T?) rule!.Cast(raw) : default;
        }

        return default;
    }

    public object? ReadAny(int channel) {
        if (!_store.HasAny(channel)) return null;
        return _store.GetAny(channel, BucketType(channel));
    }

    public Type? TypeOf(int channel) {
        if (channel < 0 || channel >= _channelTypes.Length) return null;
        return _channelTypes[channel];
    }

    public bool Has<T>(PipelineBuilder.NodeInstance node, IPort port) {
        return Has<T>(node.Mappings[port]);
    }

    public bool Has<T>(int channel) {
        if (!_store.HasAny(channel)) return false;
        var declaredType = _channelTypes[channel];
        if (declaredType == typeof(T)) return true;
        if (declaredType.IsAssignableTo(typeof(T))) return true;
        return _castRegistry.CanCast(declaredType, typeof(T));
    }

    public bool HasAny(int channel) {
        return _store.HasAny(channel);
    }

    public void WriteAt<T>(PipelineBuilder.NodeInstance node, IArrayInputPortMarker port, int index, T? value) {
        var channel = node.Mappings[(IPort) port];
        var existing = _store.Get<T[]>(channel);
        T[] array;

        if (existing is not null) {
            array = existing;
        } else if (node.ArrayCounts?.TryGetValue((IPort) port, out var count) == true) {
            array = new T[count];
        } else {
            array = new T[index + 1];
        }

        if (index >= array.Length) {
            var resized = new T[index + 1];
            Array.Copy(array, resized, array.Length);
            array = resized;
        }

        array[index] = value!;
        _store.Set(channel, array);

        var requiredWords = (array.Length + 31) >> 5;
        if (!_writeAtMasks.TryGetValue(channel, out var mask) || mask.Length < requiredWords) {
            var grown = new int[requiredWords];
            if (mask is not null)
                Array.Copy(mask, grown, Math.Min(mask.Length, requiredWords));
            _writeAtMasks[channel] = grown;
            mask = grown;
        }
        mask[index >> 5] |= 1 << (index & 31);
    }

    /// <inheritdoc/>
    public int[]? GetSuppliedMask(int channel) {
        return _writeAtMasks.TryGetValue(channel, out var mask) ? mask : null;
    }

    /// <inheritdoc/>
    public void SetSuppliedMask(int channel, int[]? mask) {
        if (mask is null) {
            _writeAtMasks.Remove(channel);
            return;
        }
        _writeAtMasks[channel] = mask;
    }
}
