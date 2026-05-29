using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Registry for custom <see cref="JsonConverter"/>s applied to the cache's <see cref="JsonSerializerOptions"/>.
/// Supports both direct converter instances and open-generic converter types.
/// </summary>
public sealed class CacheTypeAdapterRegistry {
    private readonly List<JsonConverter> _converters = [];
    public IReadOnlyList<JsonConverter> Converters => _converters;

    /// <summary>Register a concrete <see cref="JsonConverter"/> instance.</summary>
    public void Register(JsonConverter converter) {
        _converters.Add(converter);
    }

    /// <summary>Register an open generic <see cref="JsonConverter{T}"/> type. Closed types are created on demand.</summary>
    public void Register(Type openGenericConverterType) {
        Console.WriteLine($"Registering {openGenericConverterType.FullName} as cache type adapter.");

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

    public void FromAttributes() {
        var attributeAssembly = typeof(CacheTypeAdapterAttribute).Assembly;
        var attributeAssemblyName = attributeAssembly.GetName();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            var isAttributeAssembly = assembly == attributeAssembly;
            var referencesAttribute = assembly.GetReferencedAssemblies()
                .Any(a => a.FullName == attributeAssemblyName.FullName);

            if (!isAttributeAssembly && !referencesAttribute) continue;

            try {
                foreach (var type in assembly.GetTypes()) {
                    if (!type.IsDefined(typeof(CacheTypeAdapterAttribute), false)) continue;
                    if (!typeof(JsonConverter).IsAssignableFrom(type) && !type.IsGenericTypeDefinition)
                        throw new InvalidOperationException($"Type {type.FullName} is not a JsonConverter.");

                    if (type.IsGenericTypeDefinition) {
                        Register(type);
                    } else {
                        try {
                            var instance = (JsonConverter?) Activator.CreateInstance(type);
                            if (instance is null) throw new InvalidOperationException($"Failed to create instance of {type.FullName}.");

                            Register(instance);
                        } catch (Exception ex) {
                            throw new InvalidOperationException($"Failed to create instance of {type.FullName}.", ex);
                        }
                    }
                }
            } catch (ReflectionTypeLoadException e) {
                var types = e.Types.Where(t => t != null).ToArray();
                Console.WriteLine($"Failed to load types from assembly {assembly.FullName}: {string.Join(", ", types.Select(t => t.FullName))}");
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class CacheTypeAdapterAttribute : Attribute {
}
