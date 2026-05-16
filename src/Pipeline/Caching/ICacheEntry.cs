namespace Shiron.Lib.Pipeline.Caching;

public interface ICacheEntry {
    IReadOnlyDictionary<string, CachePortValue> Inputs { get; }
    IReadOnlyDictionary<string, CachePortValue> Outputs { get; }
    string NodeTypeName { get; }
    DateTimeOffset CachedAt { get; }
}
