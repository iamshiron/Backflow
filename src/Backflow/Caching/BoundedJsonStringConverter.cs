using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Backflow.Caching;

internal sealed class BoundedJsonStringConverter(int limit) : JsonConverter<string> {
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();
    public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString()!;

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) {
        Validate(value);
        writer.WriteStringValue(value);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) {
        Validate(value);
        writer.WritePropertyName(value);
    }

    private void Validate(string value) {
        if (value.Length > limit) throw new InvalidDataException("String cannot fit in the configured cache byte budget.");
    }
}
