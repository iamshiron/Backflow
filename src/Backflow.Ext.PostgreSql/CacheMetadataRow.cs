using Shiron.Backflow.Caching;

namespace Shiron.Backflow.Ext.PostgreSql;

internal sealed record CacheMetadataRow(Guid Revision, SerializedCacheEntry? Snapshot);
