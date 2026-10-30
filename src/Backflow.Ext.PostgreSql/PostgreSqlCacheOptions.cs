using System.Text;

namespace Shiron.Backflow.Ext.PostgreSql;

/// <summary>Immutable namespace policy and resource limits for PostgreSQL cache metadata.</summary>
public sealed record PostgreSqlCacheOptions {
    /// <summary>Provisioned schema, restricted to lowercase ASCII SQL identifiers.</summary>
    public string Schema { get; init; } = "backflow_cache";
    /// <summary>Independent cache namespace. All writers must use the same capacity and lifetime policy.</summary>
    public string Scope { get; init; } = "default";
    /// <summary>Maximum committed entries in this namespace.</summary>
    public int MaximumEntries { get; init; } = 1024;
    /// <summary>Maximum aggregate committed payload bytes in this namespace.</summary>
    public long MaximumTotalBytes { get; init; } = 64 * 1024 * 1024;
    /// <summary>Maximum emitted/accepted JSON payload bytes per entry, at most 16 MiB.</summary>
    public int MaximumEntryBytes { get; init; } = 1024 * 1024;
    /// <summary>Maximum lifetime from the original entry creation time, at most one year.</summary>
    public TimeSpan MaximumAge { get; init; } = TimeSpan.FromDays(7);
    /// <summary>Finite total timeout, including queueing, serialization, connection acquisition and database locks.</summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum concurrent operations and serialized working buffers per adapter instance.</summary>
    public int MaximumConcurrentOperations { get; init; } = 8;
    /// <summary>Application-owned DTO types allowed when reopening persisted entries.</summary>
    public IReadOnlyCollection<Type> AllowedTypes { get; init; } = [];

    internal void Validate() {
        ValidateSchema(Schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(Scope);
        if (Encoding.UTF8.GetByteCount(Scope) > 256 || Scope.Contains('\0')) throw new ArgumentException("Cache scope exceeds 256 UTF-8 bytes or contains NUL.");
        if (MaximumEntries <= 0 || MaximumTotalBytes <= 0 || MaximumEntryBytes <= 0 || MaximumEntryBytes > PostgreSqlCacheProvisioner.MaximumPayloadBytes ||
            MaximumAge <= TimeSpan.Zero || MaximumAge > TimeSpan.FromDays(365) || OperationTimeout <= TimeSpan.Zero ||
            OperationTimeout > TimeSpan.FromHours(1) || MaximumConcurrentOperations <= 0 || MaximumConcurrentOperations > 256)
            throw new ArgumentOutOfRangeException(nameof(PostgreSqlCacheOptions));
    }

    internal static void ValidateSchema(string schema) {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        if (schema.Length > 63 || schema[0] is < 'a' or > 'z' || schema.Any(c => c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_')))
            throw new ArgumentException("Schema must be a lowercase ASCII SQL identifier of at most 63 characters.", nameof(schema));
    }
}
