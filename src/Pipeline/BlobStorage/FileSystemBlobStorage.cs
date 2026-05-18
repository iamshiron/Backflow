using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiron.Lib.Pipeline.BlobStorage;

public sealed class FileSystemBlobStorage : IBlobStorage {
    public string Name { get; }
    private readonly string _baseDirectory;
    private readonly string _registryPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, RegistryEntry> _registry = new();
    private bool _registryLoaded;

    public FileSystemBlobStorage(string name, string baseDirectory) {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _registryPath = Path.Combine(_baseDirectory, "registry.json");
        Directory.CreateDirectory(_baseDirectory);
    }

    public async ValueTask<string> StoreAsync(Stream data, BlobMetadata? metadata = null, CancellationToken ct = default) {
        var blobId = Guid.NewGuid().ToString("N");
        var blobPath = GetBlobPath(blobId);
        Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);

        await _lock.WaitAsync(ct);
        try {
            await using var fileStream = new FileStream(blobPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 8192, useAsync: true);
            await data.CopyToAsync(fileStream, ct);
        } finally {
            _lock.Release();
        }

        var actualLength = new FileInfo(blobPath).Length;
        var detectedContentType = metadata?.ContentType ?? DetectContentType(blobPath);

        var effectiveMeta = metadata is not null
            ? metadata with { ContentLength = metadata.ContentLength ?? actualLength, ContentType = metadata.ContentType ?? detectedContentType }
            : new BlobMetadata { ContentLength = actualLength, ContentType = detectedContentType };

        await SetRegistryEntryAsync(blobId, effectiveMeta, ct);

        return blobId;
    }

    public async ValueTask<Stream> OpenReadAsync(string blobId, CancellationToken ct = default) {
        var blobPath = GetBlobPath(blobId);
        if (!File.Exists(blobPath))
            throw new FileNotFoundException($"Blob '{blobId}' not found.", blobPath);

        await _lock.WaitAsync(ct);
        try {
            return new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 8192, useAsync: true);
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask<BlobMetadata?> GetMetadataAsync(string blobId, CancellationToken ct = default) {
        await EnsureRegistryLoadedAsync(ct);
        return _registry.TryGetValue(blobId, out var entry) ? entry.Metadata : null;
    }

    public async ValueTask<bool> ExistsAsync(string blobId, CancellationToken ct = default) {
        return File.Exists(GetBlobPath(blobId));
    }

    public async ValueTask<bool> RemoveAsync(string blobId, CancellationToken ct = default) {
        var blobPath = GetBlobPath(blobId);

        await _lock.WaitAsync(ct);
        try {
            var existed = false;
            if (File.Exists(blobPath)) {
                File.Delete(blobPath);
                existed = true;
            }

            var prefixDir = Path.GetDirectoryName(blobPath);
            if (prefixDir is not null && Directory.Exists(prefixDir) && !Directory.EnumerateFileSystemEntries(prefixDir).Any()) {
                Directory.Delete(prefixDir);
            }

            if (existed) {
                _registry.TryRemove(blobId, out _);
                await PersistRegistryAsync(ct);
            }

            return existed;
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask ClearAsync(CancellationToken ct = default) {
        await _lock.WaitAsync(ct);
        try {
            foreach (var dir in Directory.GetDirectories(_baseDirectory)) {
                Directory.Delete(dir, true);
            }

            _registry.Clear();
            _registryLoaded = true;
            if (File.Exists(_registryPath)) {
                File.Delete(_registryPath);
            }
        } finally {
            _lock.Release();
        }
    }

    public void Dispose() {
        _lock.Dispose();
    }

    public ValueTask DisposeAsync() {
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }

    private string GetBlobPath(string blobId) {
        var prefix = blobId.Length >= 2 ? blobId[..2] : blobId;
        return Path.Combine(_baseDirectory, prefix, blobId);
    }

    private async Task EnsureRegistryLoadedAsync(CancellationToken ct) {
        if (_registryLoaded) return;

        await _lock.WaitAsync(ct);
        try {
            if (_registryLoaded) return;

            if (File.Exists(_registryPath)) {
                var json = await File.ReadAllTextAsync(_registryPath, ct);
                var entries = JsonSerializer.Deserialize<Dictionary<string, RegistryEntry>>(json);
                if (entries is not null) {
                    foreach (var kvp in entries) {
                        _registry[kvp.Key] = kvp.Value;
                    }
                }
            }

            _registryLoaded = true;
        } finally {
            _lock.Release();
        }
    }

    private async Task SetRegistryEntryAsync(string blobId, BlobMetadata metadata, CancellationToken ct) {
        await EnsureRegistryLoadedAsync(ct);

        _registry[blobId] = new RegistryEntry(metadata);

        await _lock.WaitAsync(ct);
        try {
            await PersistRegistryAsync(ct);
        } finally {
            _lock.Release();
        }
    }

    private static readonly JsonSerializerOptions RegistryJsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task PersistRegistryAsync(CancellationToken ct) {
        var json = JsonSerializer.Serialize(_registry, RegistryJsonOptions);
        await File.WriteAllTextAsync(_registryPath, json, ct);
    }

    private static string? DetectContentType(string path) {
        try {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 16);
            Span<byte> header = stackalloc byte[16];
            var read = fs.Read(header);
            return read > 0 ? DetectContentTypeFromHeader(header[..read]) : null;
        } catch {
            return null;
        }
    }

    private static string? DetectContentTypeFromHeader(ReadOnlySpan<byte> h) {
        if (h.Length >= 4 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return "image/png";
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return "image/jpeg";
        if (h.Length >= 4 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) return "image/gif";
        if (h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
            && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50) return "image/webp";
        if (h.Length >= 4 && h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46) return "application/pdf";
        if (h.Length >= 2 && h[0] == 0x1F && h[1] == 0x8B) return "application/gzip";
        if (h.Length >= 4 && h[0] == 0x50 && h[1] == 0x4B && h[2] == 0x03 && h[3] == 0x04) return "application/zip";
        return null;
    }

    private sealed record RegistryEntry(BlobMetadata Metadata);
}
