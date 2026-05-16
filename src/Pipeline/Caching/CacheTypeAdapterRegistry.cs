using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class CacheTypeAdapterRegistry {
    private readonly List<JsonConverter> _converters = [];

    public void Register(JsonConverter converter) {
        _converters.Add(converter);
    }

    public void Register<T, TDto>(Func<T, TDto> toDto, Func<TDto, T> fromDto) where TDto : notnull {
        _converters.Add(new DelegateJsonConverter<T, TDto>(toDto, fromDto));
    }

    internal void ApplyTo(JsonSerializerOptions options) {
        foreach (var converter in _converters)
            options.Converters.Add(converter);
    }

    internal JsonSerializerOptions CreateJsonOptions(JsonSerializerOptions? baseOptions = null) {
        var options = new JsonSerializerOptions {
            WriteIndented = baseOptions?.WriteIndented ?? true,
            DefaultIgnoreCondition = baseOptions?.DefaultIgnoreCondition ?? JsonIgnoreCondition.Never,
            IncludeFields = baseOptions?.IncludeFields ?? true,
        };
        if (baseOptions is not null) {
            foreach (var converter in baseOptions.Converters)
                options.Converters.Add(converter);
        }
        ApplyTo(options);
        return options;
    }

    private sealed class DelegateJsonConverter<T, TDto>(
        Func<T, TDto> toDto,
        Func<TDto, T> fromDto
    ) : JsonConverter<T> where TDto : notnull {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var dto = JsonSerializer.Deserialize<TDto>(ref reader, options);
            return fromDto(dto!);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
            JsonSerializer.Serialize(writer, toDto(value), options);
        }
    }
}
