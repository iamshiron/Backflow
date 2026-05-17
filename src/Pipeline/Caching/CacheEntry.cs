namespace Shiron.Lib.Pipeline.Caching;

/// <summary>Default <see cref="ICacheEntry"/> implementation.</summary>
public sealed class CacheEntry : ICacheEntry {
    public required IReadOnlyDictionary<string, CachePortValue> Inputs { get; init; }
    public required IReadOnlyDictionary<string, CachePortValue> Outputs { get; init; }
    public required string NodeTypeName { get; init; }
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
}
