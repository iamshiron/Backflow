using System.Text.Json;
using System.Text.Json.Serialization;
using Shiron.Lib.Pipeline.Caching;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

// These tests exercise CacheTypeAdapterRegistry.FromAttributes(), which scans loaded
// assemblies for types decorated with [CacheTypeAdapter].
//
// IMPORTANT: FromAttributes has a bug on line 86 where it compares
//   attributeAssemblyName = attributeAssembly.GetName().FullName
// with a.Name (simple name) in the reference-check. This prevents external assemblies
// from being scanned. The fix is to change .FullName to .Name on line 86.
//
// The test types below are decorated with [CacheTypeAdapter]. Once the bug is fixed,
// the test assembly will be scanned and these types will be discovered.

public class CacheTypeAdapterRegistryFromAttributesTests {

    [CacheTypeAdapter]
    private class ConcreteIntConverter : JsonConverter<int> {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return reader.GetInt32();
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) {
            writer.WriteNumberValue(value);
        }
    }

    [CacheTypeAdapter]
    private class ConcreteStringConverter : JsonConverter<string> {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return reader.GetString()!;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) {
            writer.WriteStringValue(value);
        }
    }

    private readonly record struct Wrapper<T>(T Value);

    [CacheTypeAdapter]
    private class GenericWrapperConverter<T> : JsonConverter<Wrapper<T>> {
        public override Wrapper<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return JsonSerializer.Deserialize<Wrapper<T>>(ref reader, options)!;
        }

        public override void Write(Utf8JsonWriter writer, Wrapper<T> value, JsonSerializerOptions options) {
            JsonSerializer.Serialize(writer, value, options);
        }
    }

    [CacheTypeAdapter]
    private class NotAJsonConverter { }

    [Fact]
    public void FromAttributes_NonJsonConverterWithAttribute_ThrowsInvalidOperationException() {
        var registry = new CacheTypeAdapterRegistry();
        var ex = Assert.Throws<InvalidOperationException>(registry.FromAttributes);
        Assert.Contains("not a JsonConverter", ex.Message);
    }

    [Fact]
    public void FromAttributes_NonGenericConverter_FindsAndRegistersInstance() {
        var registry = new CacheTypeAdapterRegistry();
        try {
            registry.FromAttributes();
        } catch (InvalidOperationException) {
            // Thrown due to NotAJsonConverter — valid converters may already be registered
        }

        Assert.Contains(registry.Converters, c => c is ConcreteIntConverter);
        Assert.Contains(registry.Converters, c => c is ConcreteStringConverter);
    }

    [Fact]
    public void FromAttributes_GenericConverter_FindsAndRegistersAsFactory() {
        var registry = new CacheTypeAdapterRegistry();
        try {
            registry.FromAttributes();
        } catch (InvalidOperationException) { }

        Assert.Contains(registry.Converters, c => c is JsonConverterFactory);
    }

    [Fact]
    public void FromAttributes_RegisteredFactory_CanConvertTargetType() {
        var registry = new CacheTypeAdapterRegistry();
        try {
            registry.FromAttributes();
        } catch (InvalidOperationException) { }

        var factory = registry.Converters.OfType<JsonConverterFactory>().FirstOrDefault();
        Assert.NotNull(factory);
        Assert.True(factory.CanConvert(typeof(Wrapper<int>)));
    }

    [Fact]
    public void FromAttributes_RegisteredConcreteConverters_AreFunctional() {
        var registry = new CacheTypeAdapterRegistry();
        try {
            registry.FromAttributes();
        } catch (InvalidOperationException) { }

        var converter = registry.Converters.OfType<ConcreteIntConverter>().FirstOrDefault();
        Assert.NotNull(converter);

        var options = new JsonSerializerOptions();
        options.Converters.Add(converter);

        var json = JsonSerializer.Serialize(42, options);
        Assert.Equal("42", json);

        var deserialized = JsonSerializer.Deserialize<int>(json, options);
        Assert.Equal(42, deserialized);
    }
}
