namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// A cached snapshot of a node's input and output port values at execution time.
/// </summary>
public interface ICacheEntry {
    /// <summary>Input port values captured before execution.</summary>
    IReadOnlyDictionary<string, CachePortValue> Inputs { get; }
    /// <summary>Output port values captured after execution.</summary>
    IReadOnlyDictionary<string, CachePortValue> Outputs { get; }
    /// <summary>Fully-qualified type name of the node.</summary>
    string NodeTypeName { get; }
    /// <summary>When this entry was created.</summary>
    DateTimeOffset CachedAt { get; }
}
