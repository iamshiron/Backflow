using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shiron.Backflow.BlobStorage;

namespace Shiron.Backflow.Caching;

/// <summary>Thread-safe bounded snapshot serialization with explicit trusted-type deserialization.</summary>
public sealed class CacheEntrySerializer {
    private readonly int _readLimit;
    private readonly int _writeLimit;
    private readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);

    /// <summary>Creates a serializer, snapshotting allowed types and configured converters.</summary>
    public CacheEntrySerializer(CacheEntrySerializerOptions? options = null, CacheTypeAdapterRegistry? typeAdapters = null) {
        options ??= new();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumEntryBytes);
        if (options.MaximumSerializationBytes is { } writeBudget) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writeBudget);
        _readLimit = options.MaximumEntryBytes;
        _writeLimit = Math.Min(_readLimit, options.MaximumSerializationBytes ?? _readLimit);
        typeAdapters?.ApplyTo(_jsonOptions);
        _jsonOptions.Converters.Add(new BlobReferenceJsonConverter());
        _jsonOptions.Converters.Add(new BoundedJsonStringConverter(_writeLimit));
        _jsonOptions.Converters.Add(new BoundedJsonElementConverter(_writeLimit));
        _jsonOptions.Converters.Add(new BoundedJsonDocumentConverter(_writeLimit));
        Type[] defaults = [typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(string),
            typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(JsonElement),
            typeof(BlobCacheEntry), typeof(BlobReference)];
        foreach (var type in defaults) {
            RegisterType(type);
            RegisterType(type.MakeArrayType());
        }
        foreach (var type in options.AllowedTypes.ToArray()) RegisterType(type);
    }

    /// <summary>Serializes directly into a bounded destination; rejection changes no trusted-type state.</summary>
    public SerializedCacheEntry Serialize(ICacheKey key, ICacheEntry entry, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();
        var buffer = new BoundedJsonBuffer(_writeLimit, ct);
        var trusted = new HashSet<Type>();
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(EntryDto.Key));
            writer.WriteStartObject();
            WriteString(writer, nameof(KeyDto.NodeType), key.NodeType);
            WriteString(writer, nameof(KeyDto.AssemblyVersion), key.AssemblyVersion);
            WriteString(writer, nameof(KeyDto.InputHash), key.InputHash);
            WriteString(writer, nameof(KeyDto.CombinedHash), key.CombinedHash);
            writer.WriteEndObject();
            WriteString(writer, nameof(EntryDto.NodeTypeName), entry.NodeTypeName);
            writer.WriteString(nameof(EntryDto.CachedAt), entry.CachedAt);
            writer.WritePropertyName(nameof(EntryDto.Inputs));
            WritePorts(writer, entry.Inputs, trusted, ct);
            writer.WritePropertyName(nameof(EntryDto.Outputs));
            WritePorts(writer, entry.Outputs, trusted, ct);
            writer.WriteEndObject();
            writer.Flush();
        }
        ct.ThrowIfCancellationRequested();
        var payload = Encoding.UTF8.GetString(buffer.WrittenSpan);
        var result = new SerializedCacheEntry(payload, Hash(payload), entry.CachedAt);
        foreach (var type in trusted) RegisterType(type);
        return result;
    }

    /// <summary>Checks integrity and complete key identity before materializing only allowlisted types.</summary>
    public bool TryDeserialize(ICacheKey key, SerializedCacheEntry snapshot, out ICacheEntry? entry) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(snapshot);
        entry = null;
        try {
            if (snapshot.Payload is null || Encoding.UTF8.GetByteCount(snapshot.Payload) > _readLimit || Hash(snapshot.Payload) != snapshot.Checksum) return false;
            var dto = JsonSerializer.Deserialize<EntryDto>(snapshot.Payload);
            if (dto is null || dto.Key != new KeyDto(key.NodeType, key.AssemblyVersion, key.InputHash, key.CombinedHash) ||
                dto.CachedAt != snapshot.CachedAt || dto.NodeTypeName is null) return false;
            entry = new CacheEntry {
                NodeTypeName = dto.NodeTypeName, CachedAt = dto.CachedAt,
                Inputs = DecodePorts(dto.Inputs), Outputs = DecodePorts(dto.Outputs)
            };
            return true;
        } catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidDataException or ArgumentException
            or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException) {
            return false;
        }
    }

    private static string Hash(string payload) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    private void RegisterType(Type type) {
        if (type.AssemblyQualifiedName is { } qualified) _types[qualified] = type;
        if (type.FullName is { } full) _types[full] = type;
    }
    private void ValidateString(string value) {
        if (value.Length > _writeLimit) throw new InvalidDataException("String cannot fit in the configured cache byte budget.");
    }
    private void WriteString(Utf8JsonWriter writer, string name, string value) {
        ValidateString(value);
        writer.WriteString(name, value);
    }
    private void WritePorts(Utf8JsonWriter writer, IReadOnlyDictionary<string, CachePortValue> ports, HashSet<Type> trusted, CancellationToken ct) {
        writer.WriteStartObject();
        foreach (var port in ports) {
            ct.ThrowIfCancellationRequested();
            var value = port.Value;
            if (value.Value is { } instance) {
                var type = instance.GetType();
                if (value.TypeName != type.AssemblyQualifiedName && value.TypeName != type.FullName)
                    throw new ArgumentException("Cache value type name must match its runtime type.");
                trusted.Add(type);
            } else if (value.TypeName != "null" && !_types.ContainsKey(value.TypeName) &&
                !trusted.Any(t => t.AssemblyQualifiedName == value.TypeName || t.FullName == value.TypeName)) {
                throw new ArgumentException("Null cache values require an allowed type or the null marker.");
            }
            ValidateString(port.Key);
            writer.WritePropertyName(port.Key);
            writer.WriteStartObject();
            WriteString(writer, nameof(PortDto.TypeName), value.TypeName);
            writer.WritePropertyName(nameof(PortDto.Value));
            JsonSerializer.Serialize(writer, value.Value, _jsonOptions);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
    private Dictionary<string, CachePortValue> DecodePorts(Dictionary<string, PortDto> ports) {
        if (ports is null) throw new InvalidDataException("Missing cache ports.");
        return ports.ToDictionary(p => p.Key, p => {
            var port = p.Value ?? throw new InvalidDataException("Missing port value.");
            if (port.TypeName == "null" && port.Value.ValueKind == JsonValueKind.Null) return new CachePortValue(null, "null");
            if (port.TypeName is null || !_types.TryGetValue(port.TypeName, out var type)) throw new InvalidDataException("Untrusted cache type.");
            return new CachePortValue(port.Value.Deserialize(type, _jsonOptions), port.TypeName);
        }, StringComparer.Ordinal);
    }
    private sealed record KeyDto(string NodeType, string AssemblyVersion, string InputHash, string CombinedHash);
    private sealed record EntryDto(KeyDto Key, string NodeTypeName, DateTimeOffset CachedAt,
        Dictionary<string, PortDto> Inputs, Dictionary<string, PortDto> Outputs);
    private sealed record PortDto(string TypeName, JsonElement Value);
}
