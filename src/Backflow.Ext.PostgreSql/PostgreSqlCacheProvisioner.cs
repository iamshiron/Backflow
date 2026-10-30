using Npgsql;

namespace Shiron.Backflow.Ext.PostgreSql;

/// <summary>Explicit transactional provisioning of cache-only schema version one.</summary>
public static class PostgreSqlCacheProvisioner {
    internal const int MaximumPayloadBytes = 16 * 1024 * 1024;
    internal const int SchemaVersion = 1;
    private const long ProvisioningLock = 687716799003260392;

    /// <summary>Creates the isolated schema/tables/indexes, or verifies its supported version. Requires DDL privileges.</summary>
    public static async Task ProvisionAsync(NpgsqlDataSource dataSource, string schema = "backflow_cache", CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(dataSource);
        PostgreSqlCacheOptions.ValidateSchema(schema);
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 30;
        command.CommandText = $"SELECT pg_advisory_xact_lock({ProvisioningLock}); CREATE SCHEMA IF NOT EXISTS \"{schema}\";";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS "{schema}".schema_version (
                singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                version integer NOT NULL
            );
            INSERT INTO "{schema}".schema_version(singleton, version) VALUES (true, {SchemaVersion}) ON CONFLICT DO NOTHING;
            SELECT version FROM "{schema}".schema_version WHERE singleton FOR UPDATE;
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) != SchemaVersion)
            throw new InvalidOperationException("Unsupported PostgreSQL cache schema version. Provision a new schema or run a compatible migration.");
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS "{schema}".cache_scopes (
                scope_name text PRIMARY KEY CHECK (octet_length(scope_name) BETWEEN 1 AND 256),
                maximum_entries integer NOT NULL CHECK (maximum_entries > 0),
                maximum_total_bytes bigint NOT NULL CHECK (maximum_total_bytes > 0),
                maximum_entry_bytes integer NOT NULL CHECK (maximum_entry_bytes BETWEEN 1 AND {MaximumPayloadBytes}),
                maximum_age_ticks bigint NOT NULL CHECK (maximum_age_ticks > 0)
            );
            CREATE TABLE IF NOT EXISTS "{schema}".cache_entries (
                scope_name text NOT NULL REFERENCES "{schema}".cache_scopes(scope_name) ON DELETE CASCADE,
                key_hash text NOT NULL CHECK (length(key_hash) = 64 AND key_hash !~ '[^0-9a-f]'),
                revision uuid NOT NULL,
                payload bytea NOT NULL CHECK (octet_length(payload) <= {MaximumPayloadBytes}),
                checksum bytea NOT NULL CHECK (octet_length(checksum) = 32),
                cached_at timestamptz NOT NULL,
                cached_at_ticks bigint NOT NULL CHECK (cached_at_ticks BETWEEN 0 AND 3155378975999999999),
                expires_at timestamptz NOT NULL,
                payload_bytes integer GENERATED ALWAYS AS (octet_length(payload)) STORED,
                PRIMARY KEY (scope_name, key_hash)
            );
            CREATE INDEX IF NOT EXISTS cache_entries_expiration ON "{schema}".cache_entries(scope_name, expires_at);
            CREATE INDEX IF NOT EXISTS cache_entries_eviction ON "{schema}".cache_entries(scope_name, cached_at_ticks DESC, key_hash);
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
