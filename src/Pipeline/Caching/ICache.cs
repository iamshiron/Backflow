using System.Diagnostics.CodeAnalysis;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Persistent cache for pipeline node outputs. Implementations store and retrieve
/// <see cref="ICacheEntry"/> instances keyed by <see cref="ICacheKey"/>.
/// </summary>
public interface ICache : IDisposable {
    /// <summary>Look up a cached entry by key.</summary>
    ValueTask<(bool Found, ICacheEntry? Entry)> TryGetAsync(ICacheKey key);
    /// <summary>Store an entry under the given key.</summary>
    ValueTask SetAsync(ICacheKey key, ICacheEntry entry);
    /// <summary>Remove a single entry. Returns <c>true</c> if the entry existed.</summary>
    ValueTask<bool> RemoveAsync(ICacheKey key);
    /// <summary>Remove all entries.</summary>
    ValueTask ClearAsync();
}
