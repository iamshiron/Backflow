using System.Diagnostics.CodeAnalysis;

namespace Shiron.Lib.Pipeline.Caching;

public interface ICache : IDisposable {
    ValueTask<(bool Found, ICacheEntry? Entry)> TryGetAsync(ICacheKey key);
    ValueTask SetAsync(ICacheKey key, ICacheEntry entry);
    ValueTask<bool> RemoveAsync(ICacheKey key);
    ValueTask ClearAsync();
}
