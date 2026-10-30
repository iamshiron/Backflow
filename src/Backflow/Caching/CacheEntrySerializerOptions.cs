namespace Shiron.Backflow.Caching;

/// <summary>Immutable configuration for backend-independent cache serialization.</summary>
public sealed record CacheEntrySerializerOptions {
    /// <summary>Maximum accepted UTF-8 payload bytes and upper bound for serialization.</summary>
    public int MaximumEntryBytes { get; init; } = 1024 * 1024;
    /// <summary>Optional narrower write-admission budget. Never restricts deserialization of existing snapshots.</summary>
    public int? MaximumSerializationBytes { get; init; }
    /// <summary>Trusted application types permitted when reopening persisted entries.</summary>
    public IReadOnlyCollection<Type> AllowedTypes { get; init; } = [];
}
