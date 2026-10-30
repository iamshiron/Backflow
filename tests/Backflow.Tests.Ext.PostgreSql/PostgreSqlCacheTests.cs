using System.Collections;
using Shiron.Backflow.Caching;
using Shiron.Backflow.Ext.PostgreSql;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.PostgreSql;

public sealed class PostgreSqlCacheTests {
    private static CacheKey Key(string value = "a") => new("node", "1", value);
    private static CacheEntry Entry(object? value = null) => new() {
        NodeTypeName = "node", Inputs = new Dictionary<string, CachePortValue>(),
        Outputs = new Dictionary<string, CachePortValue> { ["result"] = new(value ?? 42, (value ?? 42).GetType().AssemblyQualifiedName!) }
    };

    [Theory]
    [InlineData("schema;drop table x")]
    [InlineData("a\"b")]
    [InlineData("UPPER")]
    [InlineData("_private")]
    [InlineData("")]
    public void Constructor_InvalidSchema_RejectsBeforeDatabaseUse(string schema) =>
        Assert.ThrowsAny<ArgumentException>(() => new PostgreSqlCache(new MetadataDouble(), new() { Schema = schema }));

    [Fact]
    public void Constructor_InvalidResourceLimits_Rejects() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PostgreSqlCache(new MetadataDouble(), new() { MaximumEntryBytes = int.MaxValue }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PostgreSqlCache(new MetadataDouble(), new() { OperationTimeout = Timeout.InfiniteTimeSpan }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PostgreSqlCache(new MetadataDouble(), new() { MaximumConcurrentOperations = 0 }));
    }

    [Fact]
    public async Task TryGetAsync_NotInitialized_FailsBeforeRead() {
        var store = new MetadataDouble();
        using var cache = new PostgreSqlCache(store, new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.TryGetAsync(Key()).AsTask());
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task InitializeAsync_FailedPolicyVerification_DoesNotEnableCache() {
        var store = new MetadataDouble { InitializationFailure = new InvalidOperationException("policy mismatch") };
        using var cache = new PostgreSqlCache(store, new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.InitializeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAsync(Key(), Entry()).AsTask());
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task SetAsync_MutableValue_PersistsIndependentSnapshotWithNoFlushRequired() {
        var store = new MetadataDouble();
        using var cache = new PostgreSqlCache(store, new());
        await cache.InitializeAsync();
        var values = new[] { 1, 2 };
        await cache.SetAsync(Key(), Entry(values));
        values[0] = 99;
        var restored = Assert.IsType<int[]>((await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
        Assert.Equal(1, restored[0]);
        restored[0] = 88;
        Assert.Equal(1, Assert.IsType<int[]>((await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value)[0]);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task TryGetAsync_CorruptChecksum_ConditionallyInvalidatesAndReturnsMiss() {
        var store = new MetadataDouble();
        using var cache = new PostgreSqlCache(store, new());
        await cache.InitializeAsync();
        await cache.SetAsync(Key(), Entry());
        var row = store.Rows[Key().CombinedHash];
        store.Rows[Key().CombinedHash] = row with { Snapshot = row.Snapshot! with { Payload = "{}" } };
        Assert.False((await cache.TryGetAsync(Key())).Found);
        Assert.Equal(row.Revision, store.ExpectedRevision);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task TryGetAsync_ExpiredOrOversizedRow_DropsOnlyObservedRevision() {
        var store = new MetadataDouble();
        using var cache = new PostgreSqlCache(store, new());
        await cache.InitializeAsync();
        var observed = Guid.NewGuid();
        store.Rows[Key().CombinedHash] = new(observed, null);
        store.ReplaceBeforeDelete = new(Guid.NewGuid(), new CacheEntrySerializer().Serialize(Key(), Entry(7)));
        Assert.False((await cache.TryGetAsync(Key())).Found);
        Assert.Equal(observed, store.ExpectedRevision);
        Assert.Equal(7, (await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
    }

    [Fact]
    public async Task TryGetAsync_UnknownPersistedType_NeverLoadsOrActivatesIt() {
        var store = new MetadataDouble();
        store.Rows[Key().CombinedHash] = new(Guid.NewGuid(), new CacheEntrySerializer().Serialize(Key(), Entry(new ApplicationResult(9))));
        using var cache = new PostgreSqlCache(store, new());
        await cache.InitializeAsync();
        Assert.False((await cache.TryGetAsync(Key())).Found);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task TryGetAsync_ReopenedWithExplicitAllowedType_RestoresDto() {
        var store = new MetadataDouble();
        using (var writer = new PostgreSqlCache(store, new())) {
            await writer.InitializeAsync();
            await writer.SetAsync(Key(), Entry(new ApplicationResult(9)));
        }
        using var reader = new PostgreSqlCache(store, new() { AllowedTypes = [typeof(ApplicationResult)] });
        await reader.InitializeAsync();
        Assert.Equal(new ApplicationResult(9), (await reader.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
    }

    [Fact]
    public async Task TryGetAsync_CompleteKeyMismatch_NeverReturnsFalseHit() {
        var store = new MetadataDouble();
        store.Rows[Key().CombinedHash] = new(Guid.NewGuid(), new CacheEntrySerializer().Serialize(Key("different"), Entry()));
        using var cache = new PostgreSqlCache(store, new());
        await cache.InitializeAsync();
        Assert.False((await cache.TryGetAsync(Key())).Found);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetAsync_OversizedLazyOutput_StopsBeforeReplacingCommittedValue(bool limitTotalBytes) {
        var store = new MetadataDouble();
        using var cache = new PostgreSqlCache(store, new() {
            MaximumEntryBytes = limitTotalBytes ? 64 * 1024 : 1024,
            MaximumTotalBytes = limitTotalBytes ? 1024 : 64 * 1024 * 1024
        });
        await cache.InitializeAsync();
        await cache.SetAsync(Key(), Entry());
        var values = new CountingValues();
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.SetAsync(Key(), Entry(values)).AsTask());
        Assert.InRange(values.Visited, 1, 4096);
        Assert.Equal(1, store.Writes);
        Assert.Equal(42, (await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
    }

    [Fact]
    public async Task TryGetAsync_DatabaseFailure_PropagatesRatherThanInventingMiss() {
        using var cache = new PostgreSqlCache(new MetadataDouble { ReadFailure = new IOException("unavailable") }, new());
        await cache.InitializeAsync();
        await Assert.ThrowsAsync<IOException>(() => cache.TryGetAsync(Key()).AsTask());
    }

    [Fact]
    public async Task TryGetAsync_OperationTimeout_CancelsDatabaseBoundary() {
        using var cache = new PostgreSqlCache(new MetadataDouble { BlockReads = true }, new() { OperationTimeout = TimeSpan.FromMilliseconds(50) });
        await cache.InitializeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.TryGetAsync(Key()).AsTask());
    }

    [Fact]
    public async Task SetAsync_CanceledWhileQueued_DoesNotSerializeOrWrite() {
        var store = new MetadataDouble { BlockReads = true };
        using var cache = new PostgreSqlCache(store, new() { MaximumConcurrentOperations = 1 });
        await cache.InitializeAsync();
        using var readerCancellation = new CancellationTokenSource();
        var pending = cache.TryGetAsync(Key(), readerCancellation.Token).AsTask();
        await store.ReadStarted.Task;
        using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var values = new CountingValues();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.SetAsync(Key(), Entry(values), canceled.Token).AsTask());
        Assert.Equal(0, values.Visited);
        Assert.Equal(0, store.Writes);
        readerCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Dispose_ActiveRead_CancelsAndRejectsFurtherCalls() {
        var store = new MetadataDouble { BlockReads = true };
        var cache = new PostgreSqlCache(store, new());
        await cache.InitializeAsync();
        var pending = cache.TryGetAsync(Key()).AsTask();
        await store.ReadStarted.Task;
        cache.Dispose();
        cache.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.TryGetAsync(Key()).AsTask());
    }

    [Fact]
    public async Task ICache_ExistingContract_StoresRemovesAndClears() {
        var implementation = new PostgreSqlCache(new MetadataDouble(), new());
        await implementation.InitializeAsync();
        using ICache cache = implementation;
        await cache.SetAsync(Key(), Entry());
        Assert.True((await cache.TryGetAsync(Key())).Found);
        Assert.True(await cache.RemoveAsync(Key()));
        await cache.SetAsync(Key(), Entry());
        await cache.ClearAsync();
        Assert.False((await cache.TryGetAsync(Key())).Found);
    }

    private sealed record ApplicationResult(int Value);
    private sealed class CountingValues : IEnumerable<int> {
        public int Visited { get; private set; }
        public IEnumerator<int> GetEnumerator() {
            for (var i = 0; i < 1_000_000; i++) { Visited++; yield return i; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class MetadataDouble : ICacheMetadataStore {
        public Dictionary<string, CacheMetadataRow> Rows { get; } = [];
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Guid? ExpectedRevision { get; private set; }
        public CacheMetadataRow? ReplaceBeforeDelete { get; set; }
        public Exception? InitializationFailure { get; init; }
        public Exception? ReadFailure { get; init; }
        public bool BlockReads { get; init; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task InitializeAsync(CancellationToken ct) => InitializationFailure is { } ex ? Task.FromException(ex) : Task.CompletedTask;
        public async Task<CacheMetadataRow?> ReadAsync(string hash, CancellationToken ct) {
            Reads++;
            ReadStarted.TrySetResult();
            if (BlockReads) await Task.Delay(Timeout.Infinite, ct);
            if (ReadFailure is { } ex) throw ex;
            return Rows.GetValueOrDefault(hash);
        }
        public Task WriteAsync(string hash, SerializedCacheEntry snapshot, CancellationToken ct) {
            Writes++;
            Rows[hash] = new(Guid.NewGuid(), snapshot);
            return Task.CompletedTask;
        }
        public Task<bool> RemoveAsync(string hash, Guid? expectedRevision, CancellationToken ct) {
            ExpectedRevision = expectedRevision;
            if (ReplaceBeforeDelete is { } replacement) { Rows[hash] = replacement; ReplaceBeforeDelete = null; }
            return Task.FromResult((expectedRevision is null || Rows.GetValueOrDefault(hash)?.Revision == expectedRevision) && Rows.Remove(hash));
        }
        public Task ClearAsync(CancellationToken ct) { Rows.Clear(); return Task.CompletedTask; }
        public Task<int> PruneAsync(CancellationToken ct) => Task.FromResult(0);
    }
}
