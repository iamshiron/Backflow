namespace Shiron.Lib.Pipeline.BlobStorage;

public record BlobMetadata {
    public string? ContentType { get; init; }
    public long? ContentLength { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}
