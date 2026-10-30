namespace Shiron.Backflow.Caching;

/// <summary>Default <see cref="ICacheEntry"/> implementation.</summary>
public sealed class CacheEntry : ICacheEntry {
    /// <inheritdoc/>
    public required IReadOnlyDictionary<string, CachePortValue> Inputs { get; init; }
    /// <inheritdoc/>
    public required IReadOnlyDictionary<string, CachePortValue> Outputs { get; init; }
    /// <inheritdoc/>
    public required string NodeTypeName { get; init; }
    /// <inheritdoc/>
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
}
