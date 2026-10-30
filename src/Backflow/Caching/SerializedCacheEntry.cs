namespace Shiron.Backflow.Caching;

/// <summary>A bounded, checksummed cache snapshot, independent of its persistence backend.</summary>
/// <param name="Payload">UTF-8 JSON represented as a string.</param>
/// <param name="Checksum">Lowercase SHA-256 of the UTF-8 payload.</param>
/// <param name="CachedAt">Original creation time, also verified against the payload.</param>
public sealed record SerializedCacheEntry(string Payload, string Checksum, DateTimeOffset CachedAt);
