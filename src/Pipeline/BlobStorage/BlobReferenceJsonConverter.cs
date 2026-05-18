using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.BlobStorage;

public sealed class BlobReferenceJsonConverter : JsonConverter<BlobReference> {
    public override BlobReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var uriString = reader.GetString();
        if (uriString is null)
            throw new JsonException("Expected a non-null string for BlobReference.");

        if (!BlobReference.TryParse(uriString, out var reference))
            throw new JsonException($"Invalid blob URI: '{uriString}'.");

        return reference;
    }

    public override void Write(Utf8JsonWriter writer, BlobReference value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Uri.ToString());
    }
}
