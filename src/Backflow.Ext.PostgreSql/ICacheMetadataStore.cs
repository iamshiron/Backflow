using Shiron.Backflow.Caching;

namespace Shiron.Backflow.Ext.PostgreSql;

internal interface ICacheMetadataStore {
    Task InitializeAsync(CancellationToken ct);
    Task<CacheMetadataRow?> ReadAsync(string hash, CancellationToken ct);
    Task WriteAsync(string hash, SerializedCacheEntry snapshot, CancellationToken ct);
    Task<bool> RemoveAsync(string hash, Guid? expectedRevision, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
    Task<int> PruneAsync(CancellationToken ct);
}
