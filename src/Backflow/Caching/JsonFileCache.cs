using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shiron.Backflow.BlobStorage;

namespace Shiron.Backflow.Caching;

/// <summary>Bounded JSON cache with private buffered writes and atomic, cross-process serialized flushes.</summary>
public sealed class JsonFileCache : ICache {
    private readonly string _filePath;
    private readonly JsonFileCacheOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, Envelope> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
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
        typeAdapters?.ApplyTo(_jsonOptions);
        _jsonOptions.Converters.Add(new BlobReferenceJsonConverter());
        Type[] defaults = [typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(string),
            typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(JsonElement),
            typeof(BlobCacheEntry), typeof(BlobReference)];
        foreach (var type in defaults) {
            RegisterType(type);
            RegisterType(type.MakeArrayType());
        }
        foreach (var type in _options.AllowedTypes) RegisterType(type);
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
            var dto = new EntryDto(new KeyDto(key.NodeType, key.AssemblyVersion, key.InputHash, key.CombinedHash),
                entry.NodeTypeName, entry.CachedAt, EncodePorts(entry.Inputs), EncodePorts(entry.Outputs));
            var payload = JsonSerializer.Serialize(dto);
            var size = Encoding.UTF8.GetByteCount(payload);
            if (size > _options.MaximumEntryBytes || size > _options.MaximumPendingBytes)
                throw new InvalidDataException("Cache entry exceeds MaximumEntryBytes.");
            _pending[key.CombinedHash] = new Envelope(payload, Hash(payload), entry.CachedAt);
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
    private static string Hash(string payload) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private void RegisterType(Type type) {
        if (type.AssemblyQualifiedName is { } qualified) _types[qualified] = type;
        if (type.FullName is { } full) _types[full] = type;
    }

    private Dictionary<string, PortDto> EncodePorts(IReadOnlyDictionary<string, CachePortValue> ports) {
        return ports.ToDictionary(p => p.Key, p => {
            var value = p.Value;
            if (value.Value is { } instance) {
                var type = instance.GetType();
                if (value.TypeName != type.AssemblyQualifiedName && value.TypeName != type.FullName)
                    throw new ArgumentException("Cache value type name must match its runtime type.");
                RegisterType(type);
            } else if (value.TypeName != "null" && !_types.ContainsKey(value.TypeName)) {
                throw new ArgumentException("Null cache values require an allowed type or the null marker.");
            }
            return new PortDto(value.TypeName, JsonSerializer.SerializeToElement(value.Value, _jsonOptions));
        }, StringComparer.Ordinal);
    }

    private ICacheEntry? Decode(Envelope envelope, ICacheKey key) {
        try {
            if (envelope.Payload is null || Encoding.UTF8.GetByteCount(envelope.Payload) > _options.MaximumEntryBytes ||
                Hash(envelope.Payload) != envelope.Checksum) return null;
            var dto = JsonSerializer.Deserialize<EntryDto>(envelope.Payload);
            if (dto is null || dto.Key != new KeyDto(key.NodeType, key.AssemblyVersion, key.InputHash, key.CombinedHash) ||
                dto.CachedAt != envelope.CachedAt || dto.NodeTypeName is null) return null;
            return new CacheEntry { NodeTypeName = dto.NodeTypeName, CachedAt = dto.CachedAt,
                Inputs = DecodePorts(dto.Inputs), Outputs = DecodePorts(dto.Outputs) };
        } catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidDataException or ArgumentException) {
            return null;
        }
    }

    private Dictionary<string, CachePortValue> DecodePorts(Dictionary<string, PortDto> ports) {
        if (ports is null) throw new InvalidDataException("Missing cache ports.");
        return ports.ToDictionary(p => p.Key, p => {
            var port = p.Value ?? throw new InvalidDataException("Missing port value.");
            if (port.TypeName == "null" && port.Value.ValueKind == JsonValueKind.Null) return new CachePortValue(null, "null");
            if (port.TypeName is null || !_types.TryGetValue(port.TypeName, out var type))
                throw new InvalidDataException("Untrusted cache type.");
            return new CachePortValue(port.Value.Deserialize(type, _jsonOptions), port.TypeName);
        }, StringComparer.Ordinal);
    }

    private bool Prune(Dictionary<string, Envelope> store) {
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

    private async Task<Dictionary<string, Envelope>> ReadStoreAsync() {
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

    private async Task WriteStoreAsync(Dictionary<string, Envelope> store) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new StoreDto(2, store));
        foreach (var oldest in OldestFirst(store)) {
            if (bytes.LongLength <= _options.MaximumStoreBytes) break;
            store.Remove(oldest);
            bytes = JsonSerializer.SerializeToUtf8Bytes(new StoreDto(2, store));
        }
        await PersistentFile.WriteAsync(_filePath, bytes, _options.BeforePublish).ConfigureAwait(false);
    }

    private static string[] OldestFirst(Dictionary<string, Envelope> store) => store
        .OrderBy(e => e.Value.CachedAt).ThenByDescending(e => e.Key, StringComparer.Ordinal).Select(e => e.Key).ToArray();

    private sealed record StoreDto(int Version, Dictionary<string, Envelope> Entries);
    private sealed record Envelope(string Payload, string Checksum, DateTimeOffset CachedAt);
    private sealed record KeyDto(string NodeType, string AssemblyVersion, string InputHash, string CombinedHash);
    private sealed record EntryDto(KeyDto Key, string NodeTypeName, DateTimeOffset CachedAt,
        Dictionary<string, PortDto> Inputs, Dictionary<string, PortDto> Outputs);
    private sealed record PortDto(string TypeName, JsonElement Value);
}
