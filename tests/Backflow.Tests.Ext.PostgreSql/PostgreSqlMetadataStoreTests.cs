using Npgsql;
using Shiron.Backflow.Caching;
using Shiron.Backflow.Context;
using Shiron.Backflow.Ext.PostgreSql;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Registry;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.PostgreSql;

// Integration reason: PostgreSQL row locks, MVCC, DDL, cancellation and transaction rollback cannot be established by unit doubles.
public sealed class PostgreSqlMetadataStoreTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture> {
    private static CacheKey Key(string id = "a") => new("node", "1", id);
    private static CacheEntry Entry(int value = 42, DateTimeOffset? at = null) => new() {
        NodeTypeName = "node", CachedAt = at ?? DateTimeOffset.UtcNow,
        Inputs = new Dictionary<string, CachePortValue>(),
        Outputs = new Dictionary<string, CachePortValue> { ["result"] = new(value, typeof(int).AssemblyQualifiedName!) }
    };
    private static PostgreSqlCacheOptions Policy() => new() { Schema = "bf_" + Guid.NewGuid().ToString("N"), Scope = "integration" };
    private async Task ExecuteAsync(string sql) {
        await using var command = fixture.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    [PostgreSqlFact]
    public async Task ConcurrentInstances_TransactionalEviction_RespectCountAndByteCapsWithoutLostUpdates() {
        var options = Policy() with { MaximumEntries = 3, MaximumTotalBytes = 2048, MaximumEntryBytes = 1024 };
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        using var first = new PostgreSqlCache(fixture.DataSource, options);
        using var second = new PostgreSqlCache(fixture.DataSource, options);
        await Task.WhenAll(first.InitializeAsync(), second.InitializeAsync());
        await Task.WhenAll(Enumerable.Range(0, 24).Select(async i =>
            await (i % 2 == 0 ? first : second).SetAsync(Key(i.ToString()), Entry(i))));
        await using var command = fixture.DataSource.CreateCommand($"SELECT count(*), coalesce(sum(payload_bytes), 0) FROM \"{options.Schema}\".cache_entries");
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.InRange(reader.GetInt64(0), 1, options.MaximumEntries);
        Assert.InRange(reader.GetInt64(1), 1, options.MaximumTotalBytes);

        var independent = Policy();
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, independent.Schema);
        using var writerA = new PostgreSqlCache(fixture.DataSource, independent);
        using var writerB = new PostgreSqlCache(fixture.DataSource, independent);
        await Task.WhenAll(writerA.InitializeAsync(), writerB.InitializeAsync());
        await Task.WhenAll(Enumerable.Range(0, 24).Select(async i =>
            await (i % 2 == 0 ? writerA : writerB).SetAsync(Key(i.ToString()), Entry(i))));
        foreach (var i in Enumerable.Range(0, 24))
            Assert.Equal(i, (await writerA.TryGetAsync(Key(i.ToString()))).Entry!.Outputs["result"].Value);
    }

    [PostgreSqlFact]
    public async Task Provisioning_IsIdempotent_RejectsUnknownVersionAndConflictingScopePolicy() {
        var options = Policy();
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        using var first = new PostgreSqlCache(fixture.DataSource, options);
        await first.InitializeAsync();
        using var conflicting = new PostgreSqlCache(fixture.DataSource, options with { MaximumEntries = 9 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => conflicting.InitializeAsync());
        using var other = new PostgreSqlCache(fixture.DataSource, options with { Scope = "other" });
        await other.InitializeAsync();
        await first.SetAsync(Key(), Entry(1));
        await other.SetAsync(Key(), Entry(2));
        await first.ClearAsync();
        Assert.False((await first.TryGetAsync(Key())).Found);
        Assert.Equal(2, (await other.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => conflicting.InitializeAsync());
        await ExecuteAsync($"UPDATE \"{options.Schema}\".schema_version SET version = 999");
        await Assert.ThrowsAsync<InvalidOperationException>(() => PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema));
    }

    [PostgreSqlFact]
    public async Task CorruptExpiredAndOversizedRows_InvalidateWithoutFetchingOversizedPayloadOrDeletingReplacement() {
        var options = Policy() with { MaximumEntryBytes = 1024 };
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        using var cache = new PostgreSqlCache(fixture.DataSource, options);
        await cache.InitializeAsync();
        await cache.SetAsync(Key(), Entry());
        await ExecuteAsync($"UPDATE \"{options.Schema}\".cache_entries SET payload = convert_to('not json', 'UTF8')");
        Assert.False((await cache.TryGetAsync(Key())).Found);
        await cache.SetAsync(Key(), Entry());
        await ExecuteAsync($"UPDATE \"{options.Schema}\".cache_entries SET payload = decode(repeat('aa', 1048576), 'hex')");
        var store = new PostgreSqlMetadataStore(fixture.DataSource, options);
        var oversize = await store.ReadAsync(Key().CombinedHash, default);
        Assert.NotNull(oversize);
        Assert.Null(oversize.Snapshot);
        Assert.False((await cache.TryGetAsync(Key())).Found);
        await cache.SetAsync(Key(), Entry());
        await ExecuteAsync($"UPDATE \"{options.Schema}\".cache_entries SET expires_at = clock_timestamp() - interval '1 second'");
        Assert.Equal(1, await cache.PruneAsync());
        Assert.False((await cache.TryGetAsync(Key())).Found);
        await cache.SetAsync(Key(), Entry());
        var old = (await store.ReadAsync(Key().CombinedHash, default))!;
        await cache.SetAsync(Key(), Entry(9));
        Assert.False(await store.RemoveAsync(Key().CombinedHash, old.Revision, default));
        Assert.Equal(9, (await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
    }

    [PostgreSqlFact]
    public async Task WriteFailure_RollsBackExpirationAndReplacementInSameTransaction() {
        var options = Policy();
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        using var cache = new PostgreSqlCache(fixture.DataSource, options);
        await cache.InitializeAsync();
        await cache.SetAsync(Key(), Entry(1));
        await cache.SetAsync(Key("expired"), Entry(2));
        await ExecuteAsync($"""
            UPDATE "{options.Schema}".cache_entries SET expires_at = clock_timestamp() - interval '1 second' WHERE key_hash = '{Key("expired").CombinedHash}';
            CREATE FUNCTION "{options.Schema}".reject_write() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected failure'; END; $$;
            CREATE TRIGGER reject_write BEFORE INSERT ON "{options.Schema}".cache_entries FOR EACH ROW EXECUTE FUNCTION "{options.Schema}".reject_write();
            """);
        await Assert.ThrowsAsync<PostgresException>(() => cache.SetAsync(Key(), Entry(99)).AsTask());
        await using var count = fixture.DataSource.CreateCommand($"SELECT count(*) FROM \"{options.Schema}\".cache_entries");
        Assert.Equal(2L, await count.ExecuteScalarAsync());
        Assert.Equal(1, (await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
    }

    [PostgreSqlFact]
    public async Task CancellationDuringNamespaceLock_RollsBackAndPreservesPriorEntry() {
        var options = Policy();
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        using var cache = new PostgreSqlCache(fixture.DataSource, options);
        await cache.InitializeAsync();
        await cache.SetAsync(Key(), Entry(1));
        await using (var connection = await fixture.DataSource.OpenConnectionAsync()) {
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand($"SELECT scope_name FROM \"{options.Schema}\".cache_scopes FOR UPDATE", connection, transaction);
            await command.ExecuteScalarAsync();
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.SetAsync(Key(), Entry(2), cancel.Token).AsTask());
            await transaction.RollbackAsync();
        }
        Assert.Equal(1, (await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
        await cache.SetAsync(Key(), Entry(3));
        Assert.Equal(3, (await cache.TryGetAsync(Key())).Entry!.Outputs["result"].Value);
    }

    [PostgreSqlFact]
    public async Task NamespaceLockWait_ReadsServerClockAfterLockAcquisition() {
        var options = Policy();
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        using var cache = new PostgreSqlCache(fixture.DataSource, options);
        await cache.InitializeAsync();
        await cache.SetAsync(Key("keep"), Entry(1));
        await using (var connection = await fixture.DataSource.OpenConnectionAsync()) {
            await using var transaction = await connection.BeginTransactionAsync();
            await using var locked = new NpgsqlCommand($"SELECT scope_name FROM \"{options.Schema}\".cache_scopes FOR UPDATE", connection, transaction);
            await locked.ExecuteScalarAsync();
            var pending = cache.SetAsync(Key("waiting"), Entry(2)).AsTask();
            await Task.Delay(150);
            await using var advance = new NpgsqlCommand($"UPDATE \"{options.Schema}\".cache_entries SET cached_at = clock_timestamp()", connection, transaction);
            await advance.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            await pending;
        }
        Assert.Equal(1, (await cache.TryGetAsync(Key("keep"))).Entry!.Outputs["result"].Value);
        Assert.Equal(2, (await cache.TryGetAsync(Key("waiting"))).Entry!.Outputs["result"].Value);
    }

    [PostgreSqlFact]
    public async Task Executor_ActualImmutableContextOptions_IsolatesKeysAndRestoresAfterNewConnectionPool() {
        var options = Policy();
        await PostgreSqlCacheProvisioner.ProvisionAsync(fixture.DataSource, options.Schema);
        var node = new OffsetNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var instance = builder.AddNode(node);
        var pipeline = builder.Build();
        var first = ArrayPipelineContext.ForPipeline(pipeline, new ExecutionOptions(3));
        var second = ArrayPipelineContext.ForPipeline(pipeline, new ExecutionOptions(8));
        first.Write(instance, node.InputValue, 10);
        second.Write(instance, node.InputValue, 10);
        using (var cache = new PostgreSqlCache(fixture.DataSource, options)) {
            await cache.InitializeAsync();
            var executor = new PipelineExecutor(pipeline, cache);
            Assert.Equal(0, (await executor.ExecuteValueAsync(first)).CacheHits);
            Assert.Equal(13, first.Read<int>(instance, node.Result));
            Assert.Equal(0, (await executor.ExecuteValueAsync(second)).CacheHits);
            Assert.Equal(18, second.Read<int>(instance, node.Result));
        }
        await using var freshPool = NpgsqlDataSource.Create(fixture.ConnectionString);
        using var reopened = new PostgreSqlCache(freshPool, options);
        await reopened.InitializeAsync();
        Assert.Equal(1, (await new PipelineExecutor(pipeline, reopened).ExecuteValueAsync(first)).CacheHits);
        Assert.Equal(2, node.Executions);
        Assert.Equal(13, first.Read<int>(instance, node.Result));
    }

    private sealed record ExecutionOptions(int Offset);
    private sealed class OffsetNode : AbstractNode {
        public IInputPort<int> InputValue { get; }
        public IOutputPort<int> Result { get; }
        public int Executions { get; private set; }
        public OffsetNode() {
            UseCache = true;
            InputValue = Input(new NumericPortBuilder<int>(nameof(InputValue)).Input());
            Result = Output(new NumericPortBuilder<int>(nameof(Result)).Output());
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Executions++;
            Result.Write(context, InputValue.Read(context) + context.GetOptions<ExecutionOptions>()!.Offset);
            return ValueTask.FromResult(true);
        }
    }
}
