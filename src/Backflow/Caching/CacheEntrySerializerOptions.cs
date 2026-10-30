namespace Shiron.Backflow.Caching;

/// <summary>Immutable configuration for backend-independent cache serialization.</summary>
public sealed record CacheEntrySerializerOptions {
    /// <summary>Maximum emitted and accepted UTF-8 payload bytes.</summary>
    public int MaximumEntryBytes { get; init; } = 1024 * 1024;
    /// <summary>Trusted application types permitted when reopening persisted entries.</summary>
    public IReadOnlyCollection<Type> AllowedTypes { get; init; } = [];
}
