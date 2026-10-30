using Npgsql;
using Shiron.Backflow.Caching;

namespace Shiron.Backflow.Ext.PostgreSql;

/// <summary>Bounded transactional PostgreSQL metadata cache; blob data remains in independent blob storage.</summary>
public sealed class PostgreSqlCache : ICache, IAsyncDisposable {
    private readonly ICacheMetadataStore _store;
    private readonly CacheEntrySerializer _serializer;
    private readonly PostgreSqlCacheOptions _options;
    private readonly SemaphoreSlim _operations;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private int _disposed;
    private volatile bool _initialized;

    /// <summary>Creates an adapter using a caller-owned pooled data source. Provision and initialize before use.</summary>
    public PostgreSqlCache(NpgsqlDataSource dataSource, PostgreSqlCacheOptions? options = null, CacheTypeAdapterRegistry? typeAdapters = null)
        : this(CreateStore(dataSource, options ?? new()), options ?? new(), typeAdapters) { }

    internal PostgreSqlCache(ICacheMetadataStore store, PostgreSqlCacheOptions options, CacheTypeAdapterRegistry? typeAdapters = null) {
        options.Validate();
        _options = options;
        _store = store;
        _operations = new(options.MaximumConcurrentOperations);
        _lifetimeToken = _lifetime.Token;
        _serializer = new(new CacheEntrySerializerOptions {
            MaximumEntryBytes = (int) Math.Min(options.MaximumEntryBytes, options.MaximumTotalBytes), AllowedTypes = options.AllowedTypes
        }, typeAdapters);
    }

    /// <summary>Registers/verifies the namespace policy, without performing schema DDL.</summary>
    public async Task InitializeAsync(CancellationToken ct = default) {
        using var deadline = Deadline(ct);
        await _operations.WaitAsync(deadline.Token).ConfigureAwait(false);
        try {
            await _store.InitializeAsync(deadline.Token).ConfigureAwait(false);
            _initialized = true;
        } finally { _operations.Release(); }
    }

    /// <summary>Retrieves a verified snapshot; corrupt/expired/oversized rows are conditionally invalidated and treated as misses.</summary>
    public async ValueTask<(bool Found, ICacheEntry? Entry)> TryGetAsync(ICacheKey key, CancellationToken ct = default) {
        ValidateKey(key);
        using var deadline = Deadline(ct);
        await _operations.WaitAsync(deadline.Token).ConfigureAwait(false);
        try {
            EnsureInitialized();
            var row = await _store.ReadAsync(key.CombinedHash, deadline.Token).ConfigureAwait(false);
            if (row is null) return (false, null);
            if (row.Snapshot is { } snapshot && _serializer.TryDeserialize(key, snapshot, out var entry)) {
                deadline.Token.ThrowIfCancellationRequested();
                return (true, entry);
            }
            await _store.RemoveAsync(key.CombinedHash, row.Revision, deadline.Token).ConfigureAwait(false);
            return (false, null);
        } finally { _operations.Release(); }
    }

    /// <summary>Serializes within budget and atomically commits the snapshot plus namespace expiration/capacity eviction.</summary>
    public async ValueTask SetAsync(ICacheKey key, ICacheEntry entry, CancellationToken ct = default) {
        ValidateKey(key);
        using var deadline = Deadline(ct);
        await _operations.WaitAsync(deadline.Token).ConfigureAwait(false);
        try {
            EnsureInitialized();
            var snapshot = _serializer.Serialize(key, entry, deadline.Token);
            await _store.WriteAsync(key.CombinedHash, snapshot, deadline.Token).ConfigureAwait(false);
        } finally { _operations.Release(); }
    }

    /// <summary>Atomically removes one cache key without deleting any referenced blobs.</summary>
    public async ValueTask<bool> RemoveAsync(ICacheKey key, CancellationToken ct = default) {
        ValidateKey(key);
        using var deadline = Deadline(ct);
        await _operations.WaitAsync(deadline.Token).ConfigureAwait(false);
        try { EnsureInitialized(); return await _store.RemoveAsync(key.CombinedHash, null, deadline.Token).ConfigureAwait(false); }
        finally { _operations.Release(); }
    }

    /// <summary>Clears only this namespace, preserving its policy and independent blob storage.</summary>
    public async ValueTask ClearAsync(CancellationToken ct = default) {
        using var deadline = Deadline(ct);
        await _operations.WaitAsync(deadline.Token).ConfigureAwait(false);
        try { EnsureInitialized(); await _store.ClearAsync(deadline.Token).ConfigureAwait(false); }
        finally { _operations.Release(); }
    }

    /// <summary>Explicit maintenance for expired/over-capacity metadata when no writes occur.</summary>
    public async Task<int> PruneAsync(CancellationToken ct = default) {
        using var deadline = Deadline(ct);
        await _operations.WaitAsync(deadline.Token).ConfigureAwait(false);
        try { EnsureInitialized(); return await _store.PruneAsync(deadline.Token).ConfigureAwait(false); }
        finally { _operations.Release(); }
    }

    ValueTask<(bool Found, ICacheEntry? Entry)> ICache.TryGetAsync(ICacheKey key) => TryGetAsync(key);
    ValueTask ICache.SetAsync(ICacheKey key, ICacheEntry entry) => SetAsync(key, entry);
    ValueTask<bool> ICache.RemoveAsync(ICacheKey key) => RemoveAsync(key);
    ValueTask ICache.ClearAsync() => ClearAsync();

    /// <summary>Cancels queued/in-flight operations; does not dispose the caller's data source.</summary>
    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }

    private CancellationTokenSource Deadline(CancellationToken ct) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeToken);
        source.CancelAfter(_options.OperationTimeout);
        return source;
    }
    private void EnsureInitialized() {
        if (!_initialized) throw new InvalidOperationException("Initialize the PostgreSQL cache namespace before use.");
    }
    private static ICacheMetadataStore CreateStore(NpgsqlDataSource dataSource, PostgreSqlCacheOptions options) {
        ArgumentNullException.ThrowIfNull(dataSource);
        options.Validate();
        return new PostgreSqlMetadataStore(dataSource, options);
    }
    private static void ValidateKey(ICacheKey key) {
        ArgumentNullException.ThrowIfNull(key);
        if (key.CombinedHash is not { Length: 64 } || key.CombinedHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Cache keys require lowercase SHA-256 identities.", nameof(key));
    }
}
