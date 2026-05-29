using System.Text.Json;
using System.Text.Json.Serialization;
using Shiron.Lib.Pipeline.Caching;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Serialization;

[CacheTypeAdapter]
public class Vector2DJsonConverter<T> : JsonConverter<Vector2D<T>>
    where T : unmanaged, IFormattable, IEquatable<T>, IComparable<T> {
    public override Vector2D<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        return new Vector2D<T>(
            root.GetProperty("X").Deserialize<T>(options)!,
            root.GetProperty("Y").Deserialize<T>(options)!
        );
    }

    public override void Write(Utf8JsonWriter writer, Vector2D<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WritePropertyName("X");
        JsonSerializer.Serialize(writer, value.X, options);
        writer.WritePropertyName("Y");
        JsonSerializer.Serialize(writer, value.Y, options);
        writer.WriteEndObject();
    }
}

[CacheTypeAdapter]
public class Vector3DJsonConverter<T> : JsonConverter<Vector3D<T>>
    where T : unmanaged, IFormattable, IEquatable<T>, IComparable<T> {
    public override Vector3D<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        return new Vector3D<T>(
            root.GetProperty("X").Deserialize<T>(options)!,
            root.GetProperty("Y").Deserialize<T>(options)!,
            root.GetProperty("Z").Deserialize<T>(options)!
        );
    }

    public override void Write(Utf8JsonWriter writer, Vector3D<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WritePropertyName("X");
        JsonSerializer.Serialize(writer, value.X, options);
        writer.WritePropertyName("Y");
        JsonSerializer.Serialize(writer, value.Y, options);
        writer.WritePropertyName("Z");
        JsonSerializer.Serialize(writer, value.Z, options);
        writer.WriteEndObject();
    }
}

[CacheTypeAdapter]
public class Vector4DJsonConverter<T> : JsonConverter<Vector4D<T>>
    where T : unmanaged, IFormattable, IEquatable<T>, IComparable<T> {
    public override Vector4D<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        return new Vector4D<T>(
            root.GetProperty("X").Deserialize<T>(options)!,
            root.GetProperty("Y").Deserialize<T>(options)!,
            root.GetProperty("Z").Deserialize<T>(options)!,
            root.GetProperty("W").Deserialize<T>(options)!
        );
    }

    public override void Write(Utf8JsonWriter writer, Vector4D<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WritePropertyName("X");
        JsonSerializer.Serialize(writer, value.X, options);
        writer.WritePropertyName("Y");
        JsonSerializer.Serialize(writer, value.Y, options);
        writer.WritePropertyName("Z");
        JsonSerializer.Serialize(writer, value.Z, options);
        writer.WritePropertyName("W");
        JsonSerializer.Serialize(writer, value.W, options);
        writer.WriteEndObject();
    }
}
