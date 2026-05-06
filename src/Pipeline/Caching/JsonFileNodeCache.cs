using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// JSON-file-backed <see cref="INodeCache"/>.
/// Entries are held in memory and only persisted when <see cref="Flush"/> /
/// <see cref="FlushAsync"/> is called (or on <see cref="Dispose"/>).
/// </summary>
public sealed class JsonFileNodeCache(string filePath) : INodeCache {
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, (CacheKey Key, CacheEntry Entry)> _entries = new();
    private bool _loaded;

    public async ValueTask<CacheEntry?> Get(CacheKey key, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);
        var compositeKey = key.ToCompositeKey();
        return _entries.TryGetValue(compositeKey, out var hit) ? hit.Entry : null;
    }

    public async ValueTask Set(CacheKey key, CacheEntry entry, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);
        var compositeKey = key.ToCompositeKey();
        _entries[compositeKey] = (key, entry);
    }

    /// <summary>Persist all in-memory entries to the JSON file synchronously.</summary>
    public void Flush() {
        _fileLock.Wait();
        try {
            WriteToFile();
        } finally {
            _fileLock.Release();
        }
    }

    /// <summary>Persist all in-memory entries to the JSON file asynchronously.</summary>
    public async Task FlushAsync(CancellationToken ct = default) {
        await _fileLock.WaitAsync(ct);
        try {
            await WriteToFileAsync(ct);
        } finally {
            _fileLock.Release();
        }
    }

    public void Dispose() {
        Flush();
        _fileLock.Dispose();
    }

    public async ValueTask DisposeAsync() {
        await FlushAsync();
        _fileLock.Dispose();
    }

    private async ValueTask EnsureLoadedAsync(CancellationToken ct) {
        if (_loaded) return;
        await _fileLock.WaitAsync(ct);
        try {
            if (_loaded) return;
            await LoadFromFileAsync(ct);
            _loaded = true;
        } finally {
            _fileLock.Release();
        }
    }

    private async Task LoadFromFileAsync(CancellationToken ct) {
        if (!File.Exists(filePath)) return;

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var dto = await JsonSerializer.DeserializeAsync<CacheFileDto>(stream, JsonOptions, ct);
        if (dto?.Entries is null) return;

        foreach (var (compositeKey, entryDto) in dto.Entries) {
            var entry = FromDto(entryDto);
            var key = new CacheKey(
                entryDto.NodeType,
                entryDto.AssemblyVersion,
                entryDto.InputHash
            );
            _entries[compositeKey] = (key, entry);
        }
    }

    private void WriteToFile() {
        var dto = ToFileDto();
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        JsonSerializer.Serialize(stream, dto, JsonOptions);
    }

    private async Task WriteToFileAsync(CancellationToken ct) {
        var dto = ToFileDto();
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, ct);
    }

    private CacheFileDto ToFileDto() {
        var dict = new Dictionary<string, CacheEntryDto>();
        foreach (var (compositeKey, (key, entry)) in _entries) {
            dict[compositeKey] = new CacheEntryDto(
                key.NodeType,
                key.AssemblyVersion,
                key.InputHash,
                entry.CachedAt,
                [.. entry.Inputs],
                [.. entry.Outputs]
            );
        }
        return new CacheFileDto(dict);
    }

    private static CacheEntry FromDto(CacheEntryDto dto) {
        var entry = new CacheEntry(dto.CachedAt);

        foreach (var input in dto.Inputs) {
            var type = Type.GetType(input.TypeName) ?? typeof(object);
            var value = DeserializeValue(input.Value, type);
            entry.AddInput(input.PortName, type, value);
        }

        foreach (var output in dto.Outputs) {
            var type = Type.GetType(output.TypeName) ?? typeof(object);
            var value = DeserializeValue(output.Value, type);
            entry.AddOutput(output.PortName, type, value);
        }

        return entry;
    }

    private static object? DeserializeValue(object? raw, Type type) {
        if (raw is JsonElement je) {
            return je.Deserialize(type);
        }
        return raw;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record CacheFileDto(
        [property: JsonPropertyName("entries")] Dictionary<string, CacheEntryDto> Entries
    );

    private sealed record CacheEntryDto(
        [property: JsonPropertyName("nodeType")] string NodeType,
        [property: JsonPropertyName("assemblyVersion")] string AssemblyVersion,
        [property: JsonPropertyName("inputHash")] string InputHash,
        [property: JsonPropertyName("cachedAt")] DateTime CachedAt,
        [property: JsonPropertyName("inputs")] List<CachePortValue> Inputs,
        [property: JsonPropertyName("outputs")] List<CachePortValue> Outputs
    );
}
