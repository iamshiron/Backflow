using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class CacheTypeAdapterRegistry {
    private readonly List<JsonConverter> _converters = [];

    public void Register(JsonConverter converter) {
        _converters.Add(converter);
    }

    public void Register(Type openGenericConverterType) {
        _converters.Add(new OpenGenericJsonConverterFactory(openGenericConverterType));
    }

    internal void ApplyTo(JsonSerializerOptions options) {
        foreach (var converter in _converters)
            options.Converters.Add(converter);
    }

    private sealed class OpenGenericJsonConverterFactory : JsonConverterFactory {
        private readonly Type _openGenericType;
        private readonly ConcurrentDictionary<Type, JsonConverter?> _cache = new();

        public OpenGenericJsonConverterFactory(Type openGenericType) {
            if (openGenericType == null) throw new ArgumentNullException(nameof(openGenericType));
            if (!openGenericType.IsGenericTypeDefinition)
                throw new ArgumentException($"Type {openGenericType} is not an open generic type definition.", nameof(openGenericType));
            _openGenericType = openGenericType;
        }

        public override bool CanConvert(Type typeToConvert) {
            return CreateConverter(typeToConvert, null) != null;
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions? options) {
            return _cache.GetOrAdd(typeToConvert, static (type, self) => self.CreateConverterCore(type), this);
        }

        private JsonConverter? CreateConverterCore(Type typeToConvert) {
            if (!typeToConvert.IsConstructedGenericType) return null;

            try {
                var typeArgs = typeToConvert.GetGenericArguments();
                var concreteType = _openGenericType.MakeGenericType(typeArgs);

                if (!typeof(JsonConverter).IsAssignableFrom(concreteType)) return null;

                var converterBase = FindJsonConverterGenericBase(concreteType);
                if (converterBase is null) return null;

                var targetType = converterBase.GetGenericArguments()[0];
                if (targetType != typeToConvert) return null;

                return (JsonConverter?) Activator.CreateInstance(concreteType);
            } catch {
                return null;
            }
        }

        private static Type? FindJsonConverterGenericBase(Type type) {
            var current = type;
            while (current != null) {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(JsonConverter<>))
                    return current;
                current = current.BaseType;
            }
            return null;
        }
    }
}
