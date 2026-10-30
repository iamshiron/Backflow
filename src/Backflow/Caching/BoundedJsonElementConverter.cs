using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Backflow.Caching;

internal sealed class BoundedJsonElementConverter(int limit) : JsonConverter<JsonElement> {
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<JsonElement>(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options) {
        if (JsonMarshal.GetRawUtf8Value(value).Length > limit)
            throw new InvalidDataException("Raw JSON cannot fit in the configured cache byte budget.");
        value.WriteTo(writer);
    }
}
