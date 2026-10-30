using System.Security.Cryptography;
using System.Text.Json;
using Shiron.Backflow.Context;
using Shiron.Backflow.Types;

namespace Shiron.Backflow.Caching;

/// <summary>Hashes typed inputs and explicit semantic node/context identities using ordinal canonical JSON.</summary>
public class CacheKeyFactory : ICacheKeyFactory {
    private readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };
    private readonly CacheKeyFactoryOptions _options;

    /// <summary>Creates a factory using default serialization.</summary>
    public CacheKeyFactory() : this(null) { }

    /// <summary>Creates a factory using application converters.</summary>
    public CacheKeyFactory(CacheTypeAdapterRegistry? typeAdapters, CacheKeyFactoryOptions? options = null) {
        _options = options ?? new CacheKeyFactoryOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Version);
        typeAdapters?.ApplyTo(_jsonOptions);
    }

    /// <inheritdoc/>
    public ICacheKey CreateKey(PipelineBuilder.NodeInstance node, IPipelineContext context) {
        var type = node.Node.GetType();
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartArray();
            writer.WriteStringValue(_options.Version);
            WriteIdentityValue(writer, _options.IdentitySelector?.Invoke(node, context));
            WriteIdentity(writer, node.Node as ICacheIdentityProvider);
            WriteIdentity(writer, context as ICacheIdentityProvider);
            foreach (var port in node.Node.Inputs.OrderBy(p => p.Name, StringComparer.Ordinal)) {
                var channel = node.Mappings[port];
                var present = context.HasAny(channel);
                var value = present ? context.ReadAny(channel) : null;
                writer.WriteStartArray();
                writer.WriteStringValue(port.Name);
                writer.WriteStringValue(port.PortType?.AssemblyQualifiedName);
                writer.WriteBooleanValue(present);
                writer.WriteStringValue(value?.GetType().AssemblyQualifiedName);
                WriteValue(writer, value);
                JsonSerializer.Serialize(writer, context.GetSuppliedMask(channel), _jsonOptions);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
        var hash = Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int) buffer.Length))));
        return new CacheKey(type.AssemblyQualifiedName ?? type.Name,
            type.Assembly.GetName().Version?.ToString() ?? "0.0.0.0", hash);
    }

    private void WriteValue(Utf8JsonWriter writer, object? value) {
        switch (value) {
            case IBufferData buffer:
                writer.WriteStringValue(Convert.ToHexStringLower(SHA256.HashData(buffer.Data.Span)));
                return;
            case IStreamData data:
                using (var stream = data.OpenRead()) writer.WriteStringValue(Convert.ToHexStringLower(SHA256.HashData(stream)));
                return;
            case IBlob blob:
                writer.WriteStartArray();
                using (var stream = blob.Storage.OpenRead()) writer.WriteStringValue(Convert.ToHexStringLower(SHA256.HashData(stream)));
                foreach (var contract in blob.GetType().GetInterfaces()
                    .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IBlob<,>))
                    .OrderBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal)) {
                    writer.WriteStringValue(contract.AssemblyQualifiedName);
                    var meta = contract.GetProperty(nameof(IBlob<object, IStreamData>.Meta))!.GetValue(blob);
                    CanonicalJson.Write(writer, JsonSerializer.SerializeToElement(meta, _jsonOptions));
                }
                writer.WriteEndArray();
                return;
            default:
                CanonicalJson.Write(writer, JsonSerializer.SerializeToElement(value, _jsonOptions));
                return;
        }
    }

    private void WriteIdentity(Utf8JsonWriter writer, ICacheIdentityProvider? provider) {
        WriteIdentityValue(writer, provider?.CacheIdentity);
    }

    private void WriteIdentityValue(Utf8JsonWriter writer, object? identity) {
        writer.WriteStartArray();
        writer.WriteStringValue(identity?.GetType().AssemblyQualifiedName);
        CanonicalJson.Write(writer, JsonSerializer.SerializeToElement(identity, _jsonOptions));
        writer.WriteEndArray();
    }
}
