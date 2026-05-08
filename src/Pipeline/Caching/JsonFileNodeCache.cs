using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class JsonFileNodeCache : INodeCache {
    private readonly string _filePath;
    private readonly IBlobStore? _blobStore;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, (CacheKey Key, CacheEntry Entry)> _entries = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _blobHashes = new();
    private bool _loaded;

    public JsonFileNodeCache(string filePath, IBlobStore? blobStore = null) {
        _filePath = filePath;
        _blobStore = blobStore;
    }

    public async ValueTask<CacheEntry?> Get(CacheKey key, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);
        var compositeKey = key.ToCompositeKey();
        return _entries.TryGetValue(compositeKey, out var hit) ? hit.Entry : null;
    }

    public async ValueTask Set(CacheKey key, CacheEntry entry, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);

        if (HasBlobOutputs(entry) && _blobStore is null) {
            return;
        }

        var compositeKey = key.ToCompositeKey();

        if (_blobStore is not null) {
            var hashes = new Dictionary<string, string>();
            foreach (var output in entry.Outputs) {
                if (output.Value is IBlob blob) {
                    var hash = blob is ICacheHashable hashable
                        ? hashable.GetCacheHash()
                        : ComputeHash(blob.Data);
                    await _blobStore.StoreAsync(blob.Data, hash, ct);
                    hashes[output.PortName] = hash;
                }
            }
            if (hashes.Count > 0) {
                _blobHashes[compositeKey] = hashes;
            }
        }

        _entries[compositeKey] = (key, entry);
    }

    public void Flush() {
        _fileLock.Wait();
        try {
            WriteToFile();
        } finally {
            _fileLock.Release();
        }
    }

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
        if (!File.Exists(_filePath)) return;

        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var dto = await JsonSerializer.DeserializeAsync<CacheFileDto>(stream, JsonOptions, ct);
        if (dto?.Entries is null) return;

        foreach (var (compositeKey, entryDto) in dto.Entries) {
            var entry = await FromDtoAsync(compositeKey, entryDto, ct);
            if (entry is null) continue;

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
        using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        JsonSerializer.Serialize(stream, dto, JsonOptions);
    }

    private async Task WriteToFileAsync(CancellationToken ct) {
        var dto = ToFileDto();
        await using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, ct);
    }

    private const string BlobRefPrefix = "blob://";

    private CacheFileDto ToFileDto() {
        var dict = new Dictionary<string, CacheEntryDto>();
        foreach (var (compositeKey, (key, entry)) in _entries) {
            _blobHashes.TryGetValue(compositeKey, out var outputHashes);
            var inputDtos = new List<CachePortValue>(entry.Inputs.Count);
            var outputDtos = new List<CachePortValue>(entry.Outputs.Count);

            foreach (var input in entry.Inputs) {
                if (input.Value is ICacheHashable hashable) {
                    inputDtos.Add(new CachePortValue(
                        input.PortName,
                        hashable.GetType().FullName ?? input.TypeName,
                        $"{BlobRefPrefix}{hashable.GetCacheHash()}"
                    ));
                } else {
                    inputDtos.Add(new CachePortValue(input.PortName, input.TypeName, input.Value));
                }
            }

            foreach (var output in entry.Outputs) {
                if (outputHashes is not null && outputHashes.TryGetValue(output.PortName, out var hash)) {
                    outputDtos.Add(new CachePortValue(
                        output.PortName,
                        output.Value?.GetType().FullName ?? output.TypeName,
                        $"{BlobRefPrefix}{hash}"
                    ));
                } else {
                    outputDtos.Add(new CachePortValue(output.PortName, output.TypeName, output.Value));
                }
            }

            dict[compositeKey] = new CacheEntryDto(
                key.NodeType,
                key.AssemblyVersion,
                key.InputHash,
                entry.CachedAt,
                inputDtos,
                outputDtos
            );
        }
        return new CacheFileDto(dict);
    }

    private async Task<CacheEntry?> FromDtoAsync(string compositeKey, CacheEntryDto dto, CancellationToken ct) {
        var entry = new CacheEntry(dto.CachedAt);

        foreach (var input in dto.Inputs) {
            var type = Type.GetType(input.TypeName) ?? typeof(object);
            var value = await DeserializePortValueAsync(input.Value, type, ct);
            entry.AddInput(input.PortName, type, value);
        }

        foreach (var output in dto.Outputs) {
            var type = Type.GetType(output.TypeName) ?? typeof(object);
            var value = await DeserializePortValueAsync(output.Value, type, ct);
            if (value is not null) {
                entry.AddOutput(output.PortName, type, value);
            } else if (IsBlobType(type)) {
                return null;
            } else {
                entry.AddOutput(output.PortName, type, null);
            }
        }

        return entry;
    }

    private async ValueTask<object?> DeserializePortValueAsync(object? raw, Type type, CancellationToken ct) {
        var hash = ExtractBlobHash(raw);
        if (hash is not null) {
            if (_blobStore is null) return null;

            var data = await _blobStore.RetrieveAsync(hash, ct);
            if (data is null) return null;

            var concreteType = ResolveConcreteBlobType(type);
            var blob = (IBlob) (Activator.CreateInstance(concreteType) ?? new MemoryBlob());
            blob.Data = data;
            return blob;
        }

        if (raw is not JsonElement je) return raw;
        if (type.IsInterface || type.IsAbstract) return null;

        try {
            return je.Deserialize(type);
        } catch {
            return null;
        }
    }

    private static string? ExtractBlobHash(object? value) {
        if (value is string s && s.StartsWith(BlobRefPrefix)) {
            return s[BlobRefPrefix.Length..];
        }

        if (value is JsonElement je && je.ValueKind == JsonValueKind.String) {
            var str = je.GetString();
            if (str is not null && str.StartsWith(BlobRefPrefix)) {
                return str[BlobRefPrefix.Length..];
            }
        }

        return null;
    }

    private static bool IsBlobType(Type type) {
        return typeof(IBlob).IsAssignableFrom(type);
    }

    private static Type ResolveConcreteBlobType(Type type) {
        if (!type.IsInterface && !type.IsAbstract && typeof(IBlob).IsAssignableFrom(type)) {
            return type;
        }

        if (typeof(IImageBlob).IsAssignableFrom(type)) return typeof(MemoryImageBlob);
        if (typeof(IAudioBlob).IsAssignableFrom(type)) return typeof(MemoryAudioBlob);
        return typeof(MemoryBlob);
    }

    private static bool HasBlobOutputs(CacheEntry entry) {
        foreach (var output in entry.Outputs) {
            if (output.Value is IBlob) return true;
        }
        return false;
    }

    private static string ComputeHash(byte[] data) {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
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
