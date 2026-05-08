using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

public sealed record CachePortValue(
    [property: JsonPropertyName("portName")] string PortName,
    [property: JsonPropertyName("typeName")] string TypeName,
    [property: JsonPropertyName("value")] object? Value
);
