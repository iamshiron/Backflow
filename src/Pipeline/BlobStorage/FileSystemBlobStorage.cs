using System.Text.Json;

namespace Shiron.Lib.Pipeline.BlobStorage;

public sealed class FileSystemBlobStorage : IBlobStorage {
    public string Name { get; }
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileSystemBlobStorage(string name, string baseDirectory) {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        Directory.CreateDirectory(_baseDirectory);
    }

    public async ValueTask<string> StoreAsync(Stream data, BlobMetadata? metadata = null, CancellationToken ct = default) {
        var blobId = Guid.NewGuid().ToString("N");
        var blobPath = Path.Combine(_baseDirectory, blobId);

        await _lock.WaitAsync(ct);
        try {
            var fileStream = new FileStream(blobPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 8192, useAsync: true);
            await using (fileStream) {
                await data.CopyToAsync(fileStream, ct);
            }
        } finally {
            _lock.Release();
        }

        if (metadata is not null) {
            await WriteMetadataAsync(blobId, metadata with { ContentLength = metadata.ContentLength ?? new FileInfo(blobPath).Length }, ct);
        }

        return blobId;
    }

    public async ValueTask<Stream> OpenReadAsync(string blobId, CancellationToken ct = default) {
        var blobPath = Path.Combine(_baseDirectory, blobId);
        if (!File.Exists(blobPath))
            throw new FileNotFoundException($"Blob '{blobId}' not found.", blobPath);

        await _lock.WaitAsync(ct);
        try {
            var stream = new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 8192, useAsync: true);
            return stream;
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask<BlobMetadata?> GetMetadataAsync(string blobId, CancellationToken ct = default) {
        var metaPath = GetMetaPath(blobId);
        if (!File.Exists(metaPath)) return null;

        await _lock.WaitAsync(ct);
        try {
            var json = await File.ReadAllTextAsync(metaPath, ct);
            return JsonSerializer.Deserialize<BlobMetadata>(json);
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask<bool> ExistsAsync(string blobId, CancellationToken ct = default) {
        var blobPath = Path.Combine(_baseDirectory, blobId);
        return File.Exists(blobPath);
    }

    public async ValueTask<bool> RemoveAsync(string blobId, CancellationToken ct = default) {
        var blobPath = Path.Combine(_baseDirectory, blobId);
        var metaPath = GetMetaPath(blobId);

        await _lock.WaitAsync(ct);
        try {
            var existed = false;
            if (File.Exists(blobPath)) {
                File.Delete(blobPath);
                existed = true;
            }
            if (File.Exists(metaPath)) {
                File.Delete(metaPath);
                existed = true;
            }
            return existed;
        } finally {
            _lock.Release();
        }
    }

    public async ValueTask ClearAsync(CancellationToken ct = default) {
        await _lock.WaitAsync(ct);
        try {
            foreach (var file in Directory.GetFiles(_baseDirectory)) {
                File.Delete(file);
            }
        } finally {
            _lock.Release();
        }
    }

    private async Task WriteMetadataAsync(string blobId, BlobMetadata metadata, CancellationToken ct) {
        var metaPath = GetMetaPath(blobId);
        var json = JsonSerializer.Serialize(metadata);

        await _lock.WaitAsync(ct);
        try {
            await File.WriteAllTextAsync(metaPath, json, ct);
        } finally {
            _lock.Release();
        }
    }

    private string GetMetaPath(string blobId) => Path.Combine(_baseDirectory, $"{blobId}.meta.json");

    public void Dispose() {
        _lock.Dispose();
    }

    public ValueTask DisposeAsync() {
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
