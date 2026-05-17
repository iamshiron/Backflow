namespace Shiron.Lib.Pipeline.Caching;

/// <summary>A port's cached value paired with its assembly-qualified type name for deserialization.</summary>
public readonly record struct CachePortValue(
    object? Value,
    string TypeName
);
