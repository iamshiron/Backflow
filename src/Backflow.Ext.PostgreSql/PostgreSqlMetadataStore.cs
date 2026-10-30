using System.Data;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Shiron.Backflow.Caching;

namespace Shiron.Backflow.Ext.PostgreSql;

internal sealed class PostgreSqlMetadataStore(NpgsqlDataSource dataSource, PostgreSqlCacheOptions options) : ICacheMetadataStore {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string _scopes = $"\"{options.Schema}\".cache_scopes";
    private readonly string _entries = $"\"{options.Schema}\".cache_entries";

    public async Task InitializeAsync(CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var version = Command(connection, transaction, $"SELECT version FROM \"{options.Schema}\".schema_version WHERE singleton");
        if (Convert.ToInt32(await version.ExecuteScalarAsync(ct).ConfigureAwait(false)) != PostgreSqlCacheProvisioner.SchemaVersion)
            throw new InvalidOperationException("Unsupported PostgreSQL cache schema version.");
        await using var insert = Command(connection, transaction, $"""
            INSERT INTO {_scopes}(scope_name, maximum_entries, maximum_total_bytes, maximum_entry_bytes, maximum_age_ticks)
            VALUES (@scope, @entries, @bytes, @entrybytes, @age) ON CONFLICT DO NOTHING
            """);
        Add(insert, "entries", NpgsqlDbType.Integer, options.MaximumEntries);
        Add(insert, "bytes", NpgsqlDbType.Bigint, options.MaximumTotalBytes);
        Add(insert, "entrybytes", NpgsqlDbType.Integer, options.MaximumEntryBytes);
        Add(insert, "age", NpgsqlDbType.Bigint, options.MaximumAge.Ticks);
        await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await LockScopeAsync(connection, transaction, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<CacheMetadataRow?> ReadAsync(string hash, CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = Command(connection, null, $"""
            SELECT revision,
              CASE WHEN payload_bytes <= @limit AND expires_at > clock_timestamp() AND cached_at <= clock_timestamp()
                   THEN payload ELSE NULL END,
              checksum, cached_at_ticks
            FROM {_entries} WHERE scope_name = @scope AND key_hash = @hash
            """);
        Add(command, "hash", NpgsqlDbType.Text, hash);
        Add(command, "limit", NpgsqlDbType.Integer, (int) Math.Min(options.MaximumEntryBytes, options.MaximumTotalBytes));
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow, ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var revision = reader.GetGuid(0);
        if (await reader.IsDBNullAsync(1, ct).ConfigureAwait(false)) return new(revision, null);
        var payload = reader.GetFieldValue<byte[]>(1);
        var checksum = reader.GetFieldValue<byte[]>(2);
        var ticks = reader.GetInt64(3);
        try {
            return new(revision, new SerializedCacheEntry(StrictUtf8.GetString(payload), Convert.ToHexStringLower(checksum), new DateTimeOffset(ticks, TimeSpan.Zero)));
        } catch (Exception ex) when (ex is DecoderFallbackException or ArgumentOutOfRangeException) {
            return new(revision, null);
        }
    }

    public async Task WriteAsync(string hash, SerializedCacheEntry snapshot, CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var now = await LockScopeAsync(connection, transaction, ct).ConfigureAwait(false);
        await PruneAsync(connection, transaction, now, ct).ConfigureAwait(false);
        if (snapshot.CachedAt <= now && now - snapshot.CachedAt < options.MaximumAge) {
            await using var command = Command(connection, transaction, $"""
                INSERT INTO {_entries}(scope_name, key_hash, revision, payload, checksum, cached_at, cached_at_ticks, expires_at)
                VALUES (@scope, @hash, @revision, @payload, @checksum, @created, @ticks, @expires)
                ON CONFLICT (scope_name, key_hash) DO UPDATE SET
                  revision = EXCLUDED.revision, payload = EXCLUDED.payload, checksum = EXCLUDED.checksum,
                  cached_at = EXCLUDED.cached_at, cached_at_ticks = EXCLUDED.cached_at_ticks, expires_at = EXCLUDED.expires_at
                """);
            Add(command, "hash", NpgsqlDbType.Text, hash);
            Add(command, "revision", NpgsqlDbType.Uuid, Guid.CreateVersion7());
            Add(command, "payload", NpgsqlDbType.Bytea, Encoding.UTF8.GetBytes(snapshot.Payload));
            Add(command, "checksum", NpgsqlDbType.Bytea, Convert.FromHexString(snapshot.Checksum));
            Add(command, "created", NpgsqlDbType.TimestampTz, snapshot.CachedAt.UtcDateTime);
            Add(command, "ticks", NpgsqlDbType.Bigint, snapshot.CachedAt.UtcTicks);
            Add(command, "expires", NpgsqlDbType.TimestampTz, (snapshot.CachedAt + options.MaximumAge).UtcDateTime);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            await PruneAsync(connection, transaction, now, ct).ConfigureAwait(false);
        }
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string hash, Guid? expectedRevision, CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        if (expectedRevision is { } revision) {
            await using var conditional = Command(connection, null, $"DELETE FROM {_entries} WHERE scope_name = @scope AND key_hash = @hash AND revision = @revision");
            Add(conditional, "hash", NpgsqlDbType.Text, hash);
            Add(conditional, "revision", NpgsqlDbType.Uuid, revision);
            return await conditional.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 0;
        }
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockScopeAsync(connection, transaction, ct).ConfigureAwait(false);
        await using var command = Command(connection, transaction, $"DELETE FROM {_entries} WHERE scope_name = @scope AND key_hash = @hash");
        Add(command, "hash", NpgsqlDbType.Text, hash);
        var removed = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return removed != 0;
    }

    public async Task ClearAsync(CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockScopeAsync(connection, transaction, ct).ConfigureAwait(false);
        await using var command = Command(connection, transaction, $"DELETE FROM {_entries} WHERE scope_name = @scope");
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> PruneAsync(CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var now = await LockScopeAsync(connection, transaction, ct).ConfigureAwait(false);
        var removed = await PruneAsync(connection, transaction, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return removed;
    }

    private async Task<DateTimeOffset> LockScopeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct) {
        await using var command = Command(connection, transaction, $"""
            SELECT maximum_entries, maximum_total_bytes, maximum_entry_bytes, maximum_age_ticks
            FROM {_scopes} WHERE scope_name = @scope FOR UPDATE
            """);
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false)) {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw new InvalidOperationException("Cache namespace has not been initialized.");
            if (reader.GetInt32(0) != options.MaximumEntries || reader.GetInt64(1) != options.MaximumTotalBytes ||
                reader.GetInt32(2) != options.MaximumEntryBytes || reader.GetInt64(3) != options.MaximumAge.Ticks)
                throw new InvalidOperationException("Cache namespace policy mismatch. Use a new scope or explicitly migrate its policy while writers are stopped.");
        }
        await using var clock = Command(connection, transaction, "SELECT clock_timestamp()");
        await using var clockReader = await clock.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await clockReader.ReadAsync(ct).ConfigureAwait(false);
        return new DateTimeOffset(clockReader.GetDateTime(0));
    }

    private async Task<int> PruneAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset now, CancellationToken ct) {
        await using var expired = Command(connection, transaction, $"DELETE FROM {_entries} WHERE scope_name = @scope AND (expires_at <= @now OR cached_at > @now)");
        Add(expired, "now", NpgsqlDbType.TimestampTz, now.UtcDateTime);
        var removed = await expired.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await using var excess = Command(connection, transaction, $"""
            WITH ranked AS (
              SELECT key_hash,
                row_number() OVER (ORDER BY cached_at_ticks DESC, key_hash COLLATE "C") AS ordinal,
                sum(payload_bytes) OVER (ORDER BY cached_at_ticks DESC, key_hash COLLATE "C" ROWS UNBOUNDED PRECEDING) AS total_bytes
              FROM {_entries} WHERE scope_name = @scope
            )
            DELETE FROM {_entries} AS entry USING ranked
            WHERE entry.scope_name = @scope AND entry.key_hash = ranked.key_hash
              AND (ranked.ordinal > @count OR ranked.total_bytes > @bytes)
            """);
        Add(excess, "count", NpgsqlDbType.Integer, options.MaximumEntries);
        Add(excess, "bytes", NpgsqlDbType.Bigint, options.MaximumTotalBytes);
        return removed + await excess.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql) {
        var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = Math.Max(1, (int) Math.Ceiling(options.OperationTimeout.TotalSeconds)) };
        Add(command, "scope", NpgsqlDbType.Text, options.Scope);
        return command;
    }
    private static void Add<T>(NpgsqlCommand command, string name, NpgsqlDbType type, T value) =>
        command.Parameters.Add(new NpgsqlParameter<T>(name, type) { TypedValue = value });
}
