using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Backflow.Caching;

internal sealed class BoundedJsonDocumentConverter(int limit) : JsonConverter<JsonDocument> {
    public override JsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonDocument.ParseValue(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonDocument value, JsonSerializerOptions options) {
        if (JsonMarshal.GetRawUtf8Value(value.RootElement).Length > limit)
            throw new InvalidDataException("Raw JSON cannot fit in the configured cache byte budget.");
        value.WriteTo(writer);
    }
}
