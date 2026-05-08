using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class FileSystemBlobStore : IBlobStore {
    private readonly string _baseDir;
    private readonly string _registryPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, BlobRegistryEntry> _registry = new();
    private bool _loaded;

    public FileSystemBlobStore(string baseDir) {
        _baseDir = baseDir;
        _registryPath = Path.Combine(baseDir, "registry.json");
        Directory.CreateDirectory(baseDir);
    }

    public async ValueTask<string> StoreAsync(byte[] data, string? contentHash = null, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);

        var hash = contentHash ?? ComputeHash(data);

        if (_registry.ContainsKey(hash)) return hash;

        var path = GetBlobPath(hash);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
            await stream.WriteAsync(data, ct);
        }

        _registry[hash] = new BlobRegistryEntry(hash, DateTime.UtcNow, data.Length);

        return hash;
    }

    public async ValueTask<byte[]?> RetrieveAsync(string contentHash, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);

        var path = GetBlobPath(contentHash);
        if (!File.Exists(path)) return null;

        return await File.ReadAllBytesAsync(path, ct);
    }

    public async ValueTask<bool> ExistsAsync(string contentHash, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);
        return _registry.ContainsKey(contentHash);
    }

    public async ValueTask DeleteAsync(string contentHash, CancellationToken ct = default) {
        await EnsureLoadedAsync(ct);

        _registry.TryRemove(contentHash, out _);

        var path = GetBlobPath(contentHash);
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }

    public void Flush() {
        _lock.Wait();
        try {
            WriteRegistry();
        } finally {
            _lock.Release();
        }
    }

    public async Task FlushAsync(CancellationToken ct = default) {
        await _lock.WaitAsync(ct);
        try {
            await WriteRegistryAsync(ct);
        } finally {
            _lock.Release();
        }
    }

    public void Dispose() {
        Flush();
        _lock.Dispose();
    }

    public async ValueTask DisposeAsync() {
        await FlushAsync();
        _lock.Dispose();
    }

    private string GetBlobPath(string hash) {
        return Path.Combine(_baseDir, hash[..2], $"{hash}.blob");
    }

    private async ValueTask EnsureLoadedAsync(CancellationToken ct) {
        if (_loaded) return;
        await _lock.WaitAsync(ct);
        try {
            if (_loaded) return;
            await LoadRegistryAsync(ct);
            _loaded = true;
        } finally {
            _lock.Release();
        }
    }

    private async Task LoadRegistryAsync(CancellationToken ct) {
        if (!File.Exists(_registryPath)) return;

        await using var stream = new FileStream(_registryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var dto = await JsonSerializer.DeserializeAsync<BlobRegistryDto>(stream, JsonOptions, ct);
        if (dto?.Entries is null) return;

        foreach (var entry in dto.Entries) {
            _registry[entry.ContentHash] = entry;
        }
    }

    private void WriteRegistry() {
        var dto = new BlobRegistryDto([.. _registry.Values]);
        using var stream = new FileStream(_registryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        JsonSerializer.Serialize(stream, dto, JsonOptions);
    }

    private async Task WriteRegistryAsync(CancellationToken ct) {
        var dto = new BlobRegistryDto([.. _registry.Values]);
        await using var stream = new FileStream(_registryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, ct);
    }

    private static string ComputeHash(byte[] data) {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
    };

    private sealed record BlobRegistryDto(
        [property: JsonPropertyName("entries")] List<BlobRegistryEntry> Entries
    );
}

public sealed record BlobRegistryEntry(
    [property: JsonPropertyName("contentHash")] string ContentHash,
    [property: JsonPropertyName("storedAt")] DateTime StoredAt,
    [property: JsonPropertyName("size")] int Size
);
