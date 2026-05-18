using System.Text.Json;
using Shiron.Lib.Pipeline.BlobStorage;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class BlobReferenceJsonConverterWriteTests {
    [Fact]
    public void Write_SerializesToUriString() {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BlobReferenceJsonConverter());

        var reference = new BlobReference("disk", "abc123");
        var json = JsonSerializer.Serialize(reference, options);

        Assert.Equal("\"blob://disk/abc123\"", json);
    }
}

public class BlobReferenceJsonConverterReadTests {
    [Fact]
    public void Read_ValidUriString_ReturnsBlobReference() {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BlobReferenceJsonConverter());

        var json = "\"blob://disk/abc123\"";
        var result = JsonSerializer.Deserialize<BlobReference>(json, options);

        Assert.Equal("disk", result.StorageName);
        Assert.Equal("abc123", result.BlobId);
    }

    [Fact]
    public void Read_NullString_Throws() {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BlobReferenceJsonConverter());

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<BlobReference>("null", options));
    }

    [Fact]
    public void Read_InvalidUri_Throws() {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BlobReferenceJsonConverter());

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<BlobReference>("\"not-a-blob-uri\"", options));
    }
}

public class BlobReferenceJsonConverterRoundTripTests {
    [Fact]
    public void RoundTrip_PreservesReference() {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BlobReferenceJsonConverter());

        var original = new BlobReference("s3", "my-object-key");
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<BlobReference>(json, options);

        Assert.Equal(original, deserialized);
    }
}
