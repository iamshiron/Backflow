using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.BlobStorage;

public record BlobCacheEntry {
    public string ReferenceUri { get; init; } = "";
    public string? MetaJson { get; init; }
    public string? MetaTypeName { get; init; }

    [JsonIgnore]
    public BlobReference Reference => string.IsNullOrEmpty(ReferenceUri) ? default : BlobReference.Parse(ReferenceUri);

    internal bool HasMeta => MetaJson is not null && MetaTypeName is not null;
}
