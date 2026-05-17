using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class JsonFileCache : ICache {
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, CacheEntryDto> _pending = [];

    public JsonFileCache(string filePath, CacheTypeAdapterRegistry? typeAdapters = null) {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _jsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            IncludeFields = true,
            Converters = { new CachePortValueJsonConverter() }
        };

        typeAdapters?.ApplyTo(_jsonOptions);
    }

    public async ValueTask<(bool Found, ICacheEntry? Entry)> TryGetAsync(ICacheKey key) {
        await _lock.WaitAsync();
        try {
            if (_pending.TryGetValue(key.CombinedHash, out var pendingDto)) {
                return (true, DtoToEntry(pendingDto));
            }

            var store = await ReadStoreAsync();
            if (!store.Entries.TryGetValue(key.CombinedHash, out var dto)) {
                return (false, null);
            }
            return (true, DtoToEntry(dto));
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask SetAsync(ICacheKey key, ICacheEntry entry) {
        await _lock.WaitAsync();
        try {
            _pending[key.CombinedHash] = EntryToDto(entry, key);
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask<bool> RemoveAsync(ICacheKey key) {
        await _lock.WaitAsync();
        try {
            _pending.Remove(key.CombinedHash);
            var store = await ReadStoreAsync();
            if (!store.Entries.Remove(key.CombinedHash)) {
                return false;
            }
            await WriteStoreAsync(store);
            return true;
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask ClearAsync() {
        await _lock.WaitAsync();
        try {
            _pending.Clear();
            if (File.Exists(_filePath)) {
                File.Delete(_filePath);
            }
        } finally {
            _lock.Release();
        }
    }

    public async Task FlushAsync() {
        await _lock.WaitAsync();
        try {
            if (_pending.Count == 0) return;

            var store = await ReadStoreAsync();
            foreach (var kvp in _pending) {
                store.Entries[kvp.Key] = kvp.Value;
            }
            _pending.Clear();
            await WriteStoreAsync(store);
        } finally {
            _lock.Release();
        }
    }

    public void Flush() {
        FlushAsync().GetAwaiter().GetResult();
    }

    public void Dispose() {
        _lock.Dispose();
    }

    private async Task<CacheStoreDto> ReadStoreAsync() {
        if (!File.Exists(_filePath)) {
            return new CacheStoreDto();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json)) {
            return new CacheStoreDto();
        }

        var store = JsonSerializer.Deserialize<CacheStoreDto>(json, _jsonOptions);
        return store ?? new CacheStoreDto();
    }

    private async Task WriteStoreAsync(CacheStoreDto store) {
        var json = JsonSerializer.Serialize(store, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static CacheEntryDto EntryToDto(ICacheEntry entry, ICacheKey key) {
        return new CacheEntryDto {
            Key = new CacheKeyDto {
                NodeType = key.NodeType,
                AssemblyVersion = key.AssemblyVersion,
                InputHash = key.InputHash,
                CombinedHash = key.CombinedHash
            },
            NodeTypeName = entry.NodeTypeName,
            CachedAt = entry.CachedAt,
            Inputs = entry.Inputs.ToDictionary(
                kvp => kvp.Key,
                kvp => new CachePortValueDto { Value = kvp.Value.Value, TypeName = kvp.Value.TypeName }
            ),
            Outputs = entry.Outputs.ToDictionary(
                kvp => kvp.Key,
                kvp => new CachePortValueDto { Value = kvp.Value.Value, TypeName = kvp.Value.TypeName }
            )
        };
    }

    private static ICacheEntry DtoToEntry(CacheEntryDto dto) {
        return new CacheEntry {
            NodeTypeName = dto.NodeTypeName,
            CachedAt = dto.CachedAt,
            Inputs = dto.Inputs.ToDictionary(
                kvp => kvp.Key,
                kvp => new CachePortValue(kvp.Value.Value, kvp.Value.TypeName)
            ),
            Outputs = dto.Outputs.ToDictionary(
                kvp => kvp.Key,
                kvp => new CachePortValue(kvp.Value.Value, kvp.Value.TypeName)
            )
        };
    }

    internal sealed class CacheStoreDto {
        public Dictionary<string, CacheEntryDto> Entries { get; set; } = [];
    }

    internal sealed class CacheEntryDto {
        public CacheKeyDto Key { get; set; } = new();
        public string NodeTypeName { get; set; } = "";
        public DateTimeOffset CachedAt { get; set; }
        public Dictionary<string, CachePortValueDto> Inputs { get; set; } = [];
        public Dictionary<string, CachePortValueDto> Outputs { get; set; } = [];
    }

    internal sealed class CacheKeyDto {
        public string NodeType { get; set; } = "";
        public string AssemblyVersion { get; set; } = "";
        public string InputHash { get; set; } = "";
        public string CombinedHash { get; set; } = "";
    }

    internal sealed class CachePortValueDto {
        public object? Value { get; set; }
        public string TypeName { get; set; } = "";
    }

    private sealed class CachePortValueJsonConverter : JsonConverter<CachePortValueDto> {
        public override CachePortValueDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var typeName = root.TryGetProperty("TypeName", out var typeEl) ? typeEl.GetString() ?? "" : "";
            var valueEl = root.TryGetProperty("Value", out var vEl) ? vEl : (JsonElement?) null;

            object? value = null;
            if (valueEl is not null) {
                var type = Type.GetType(typeName);
                if (type is not null) {
                    try {
                        value = valueEl.Value.Deserialize(type, options);
                    } catch {
                        value = valueEl.Value;
                    }
                } else {
                    value = valueEl.Value;
                }
            }

            return new CachePortValueDto { Value = value, TypeName = typeName };
        }

        public override void Write(Utf8JsonWriter writer, CachePortValueDto value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            writer.WritePropertyName("Value");
            JsonSerializer.Serialize(writer, value.Value, options);
            writer.WriteString("TypeName", value.TypeName);
            writer.WriteEndObject();
        }
    }
}
