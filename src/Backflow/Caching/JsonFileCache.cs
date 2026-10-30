using System.Text;
using System.Text.Json;

namespace Shiron.Backflow.Caching;

/// <summary>Bounded JSON cache with private buffered writes and atomic, cross-process serialized flushes.</summary>
public sealed class JsonFileCache : ICache {
    private readonly string _filePath;
    private readonly JsonFileCacheOptions _options;
    private readonly CacheEntrySerializer _serializer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, SerializedCacheEntry> _pending = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Creates a cache. Dispose discards unflushed writes; call FlushAsync for persistence.</summary>
    public JsonFileCache(string filePath, CacheTypeAdapterRegistry? typeAdapters = null, JsonFileCacheOptions? options = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _options = options ?? new JsonFileCacheOptions();
        if (_options.MaximumEntries <= 0 || _options.MaximumEntryBytes <= 0 || _options.MaximumStoreBytes < 32 || _options.MaximumPendingBytes <= 0 ||
            _options.MaximumAge <= TimeSpan.Zero || _options.LockTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        _filePath = Path.GetFullPath(filePath);
        PersistentFile.CheckPath(_filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        _serializer = new CacheEntrySerializer(new CacheEntrySerializerOptions {
            MaximumEntryBytes = _options.MaximumEntryBytes,
            MaximumSerializationBytes = (int) Math.Min(_options.MaximumEntryBytes, _options.MaximumPendingBytes),
            AllowedTypes = _options.AllowedTypes
        }, typeAdapters);
    }

    /// <inheritdoc/>
    public async ValueTask<(bool Found, ICacheEntry? Entry)> TryGetAsync(ICacheKey key) {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            Prune(_pending);
            if (_pending.TryGetValue(key.CombinedHash, out var pending)) {
                var entry = Decode(pending, key);
                if (entry is not null) return (true, entry);
                _pending.Remove(key.CombinedHash);
            }
            using var lease = await AcquireAsync().ConfigureAwait(false);
            var store = await ReadStoreAsync().ConfigureAwait(false);
            var changed = Prune(store);
            ICacheEntry? result = null;
            if (store.TryGetValue(key.CombinedHash, out var envelope)) {
                result = Decode(envelope, key);
                if (result is null) changed |= store.Remove(key.CombinedHash);
            }
            if (changed) await WriteStoreAsync(store).ConfigureAwait(false);
            return (result is not null, result);
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync(ICacheKey key, ICacheEntry entry) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            _pending[key.CombinedHash] = _serializer.Serialize(key, entry);
            Prune(_pending);
            long pendingBytes = _pending.Values.Sum(e => (long) Encoding.UTF8.GetByteCount(e.Payload));
            foreach (var oldest in OldestFirst(_pending)) {
                if (pendingBytes <= _options.MaximumPendingBytes) break;
                pendingBytes -= Encoding.UTF8.GetByteCount(_pending[oldest].Payload);
                _pending.Remove(oldest);
            }
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(ICacheKey key) {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync().ConfigureAwait(false);
            var store = await ReadStoreAsync().ConfigureAwait(false);
            var removed = store.Remove(key.CombinedHash);
            if (removed) await WriteStoreAsync(store).ConfigureAwait(false);
            return _pending.Remove(key.CombinedHash) || removed;
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask ClearAsync() {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync().ConfigureAwait(false);
            PersistentFile.CheckPath(_filePath);
            File.Delete(_filePath);
            PersistentFile.SyncDirectory(Path.GetDirectoryName(_filePath)!);
            _pending.Clear();
        } finally { _lock.Release(); }
    }

    /// <summary>Publishes pending writes atomically. Failure leaves pending writes available for retry.</summary>
    public async Task FlushAsync() {
        await _lock.WaitAsync().ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync().ConfigureAwait(false);
            var store = await ReadStoreAsync().ConfigureAwait(false);
            var changed = Prune(store);
            if (_pending.Count == 0 && !changed) return;
            foreach (var pair in _pending) store[pair.Key] = pair.Value;
            Prune(store);
            await WriteStoreAsync(store).ConfigureAwait(false);
            _pending.Clear();
        } finally { _lock.Release(); }
    }

    /// <summary>Flushes pending writes synchronously.</summary>
    public void Flush() => FlushAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void Dispose() {
        _lock.Wait();
        try {
            _disposed = true;
            _pending.Clear();
        } finally { _lock.Release(); }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private ValueTask<FileStream> AcquireAsync() => PersistentFile.AcquireAsync(_filePath + ".lock", _options.LockTimeout);
    private ICacheEntry? Decode(SerializedCacheEntry envelope, ICacheKey key) =>
        _serializer.TryDeserialize(key, envelope, out var entry) ? entry : null;

    private bool Prune(Dictionary<string, SerializedCacheEntry> store) {
        var now = _options.TimeProvider.GetUtcNow();
        var removed = false;
        foreach (var pair in store.ToArray()) {
            if (pair.Value is null || pair.Value.CachedAt > now || now - pair.Value.CachedAt >= _options.MaximumAge)
                removed |= store.Remove(pair.Key);
        }
        foreach (var key in store.OrderByDescending(p => p.Value.CachedAt).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Skip(_options.MaximumEntries).Select(p => p.Key).ToArray()) removed |= store.Remove(key);
        return removed;
    }

    private async Task<Dictionary<string, SerializedCacheEntry>> ReadStoreAsync() {
        PersistentFile.CheckPath(_filePath);
        PersistentFile.RecoverTemps(_filePath);
        if (!File.Exists(_filePath)) return new(StringComparer.Ordinal);
        if (new FileInfo(_filePath).Length > _options.MaximumStoreBytes)
            throw new InvalidDataException("Cache store exceeds MaximumStoreBytes.");
        try {
            var store = JsonSerializer.Deserialize<StoreDto>(await File.ReadAllBytesAsync(_filePath).ConfigureAwait(false));
            if (store?.Version != 2 || store.Entries is null) throw new JsonException("Unsupported cache format.");
            return store.Entries;
        } catch (JsonException) {
            var quarantine = _filePath + ".corrupt";
            PersistentFile.CheckPath(quarantine);
            File.Move(_filePath, quarantine, overwrite: true);
            return new(StringComparer.Ordinal);
        }
    }

    private async Task WriteStoreAsync(Dictionary<string, SerializedCacheEntry> store) {
        var oldest = new Queue<string>(OldestFirst(store));
        while (true) {
            var buffer = new BoundedJsonBuffer((int) Math.Min(int.MaxValue, _options.MaximumStoreBytes));
            try {
                using var writer = new Utf8JsonWriter(buffer);
                JsonSerializer.Serialize(writer, new StoreDto(2, store));
                writer.Flush();
            } catch (InvalidDataException) when (oldest.Count > 0) {
                store.Remove(oldest.Dequeue());
                continue;
            }
            await PersistentFile.WriteAsync(_filePath, buffer.WrittenSpan.ToArray(), _options.BeforePublish).ConfigureAwait(false);
            return;
        }
    }

    private static string[] OldestFirst(Dictionary<string, SerializedCacheEntry> store) => store
        .OrderBy(e => e.Value.CachedAt).ThenByDescending(e => e.Key, StringComparer.Ordinal).Select(e => e.Key).ToArray();

    private sealed record StoreDto(int Version, Dictionary<string, SerializedCacheEntry> Entries);
}
