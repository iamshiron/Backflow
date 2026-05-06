using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Describes a single port's cached value for UI representation.
/// </summary>
/// <param name="PortName">Display name of the port.</param>
/// <param name="TypeName">Assembly-qualified type name of the value.</param>
/// <param name="Value">The cached value (boxed for reference types; serialization boundary only).</param>
public sealed record CachePortValue(
    [property: JsonPropertyName("portName")] string PortName,
    [property: JsonPropertyName("typeName")] string TypeName,
    [property: JsonPropertyName("value")] object? Value
);
