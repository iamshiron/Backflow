namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Abstraction over a node-execution cache.
/// Pass <c>null</c> to <see cref="PipelineExecutor"/> to disable caching entirely
/// (no reads, no writes). Implementations are free to choose their backing store
/// (JSON file, EFCore database, Redis, etc.).
/// </summary>
public interface INodeCache : IAsyncDisposable, IDisposable {
    /// <summary>
    /// Look up a previously cached result for <paramref name="key"/>.
    /// Returns <c>null</c> on miss.
    /// </summary>
    ValueTask<CacheEntry?> Get(CacheKey key, CancellationToken ct = default);

    /// <summary>
    /// Store (or overwrite) the cached result for <paramref name="key"/>.
    /// </summary>
    ValueTask Set(CacheKey key, CacheEntry entry, CancellationToken ct = default);
}
