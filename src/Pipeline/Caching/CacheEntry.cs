using System.Collections.Concurrent;
using Shiron.Lib.Collections.Bucket;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Holds the cached result of a single node execution.
/// Outputs are stored in a <see cref="ConcurrentBucketStore{T}"/> keyed by port name
/// to preserve type information and avoid boxing on typed reads.
/// </summary>
public sealed class CacheEntry {
    private readonly ConcurrentBucketStore<string> _outputStore = new();
    private readonly List<CachePortValue> _inputs = [];
    private readonly List<CachePortValue> _outputs = [];

    public DateTime CachedAt { get; }
    public IReadOnlyList<CachePortValue> Inputs => _inputs;
    public IReadOnlyList<CachePortValue> Outputs => _outputs;

    public CacheEntry(DateTime? cachedAt = null) {
        CachedAt = cachedAt ?? DateTime.UtcNow;
    }

    public void AddInput(string portName, Type type, object? value) {
        _inputs.Add(new CachePortValue(portName, type.FullName!, value));
    }

    public void AddOutput<T>(string portName, T? value) {
        _outputStore.Set(portName, value);
        _outputs.Add(new CachePortValue(portName, typeof(T).FullName!, value));
    }

    public void AddOutput(string portName, Type type, object? value) {
        _outputStore.Set(portName, value, type);
        _outputs.Add(new CachePortValue(portName, type.FullName!, value));
    }

    public T? GetOutput<T>(string portName) => _outputStore.Get<T>(portName);

    public object? GetOutputAny(string portName) => _outputStore.GetAny(portName);

    public bool HasOutput(string portName) => _outputStore.HasAny(portName);

    public Type? OutputTypeOf(string portName) => _outputStore.TypeOf(portName);

    internal IEnumerable<(string PortName, Type Type, object? Value)> GetOutputPairs() {
        foreach (var kvp in _outputStore.ValueTypes) {
            var type = kvp.Key;
            var bucket = kvp.Value;
            foreach (var key in bucket.Keys) {
                yield return (key, type, bucket.GetAny(key));
            }
        }
    }
}
