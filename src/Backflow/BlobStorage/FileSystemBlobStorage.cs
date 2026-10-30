using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Shiron.Backflow.Caching;

namespace Shiron.Backflow.BlobStorage;

/// <summary>Atomic filesystem blob storage with bounded admission and SHA-256 verification before reads.</summary>
public sealed class FileSystemBlobStorage : IBlobStorage {
    /// <inheritdoc/>
    public string Name { get; }
    private readonly string _baseDirectory;
    private readonly string _registryPath;
    private readonly FileSystemBlobStorageOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>Creates a filesystem store. Each successful write has independent ownership (no unsafe deduplication).</summary>
    public FileSystemBlobStorage(string name, string baseDirectory, FileSystemBlobStorageOptions? options = null) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        Name = name;
        _options = options ?? new FileSystemBlobStorageOptions();
        if (_options.MaximumBlobBytes <= 0 || _options.MaximumTotalBytes <= 0 || _options.MaximumBlobs <= 0 ||
            _options.MaximumRegistryBytes <= 0 || _options.LockTimeout <= TimeSpan.Zero || _options.Retention < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        _baseDirectory = Path.GetFullPath(baseDirectory);
        PersistentFile.CheckPath(_baseDirectory);
        Directory.CreateDirectory(_baseDirectory);
        _registryPath = Path.Combine(_baseDirectory, "registry.json");
    }

    /// <inheritdoc/>
    public async ValueTask<string> StoreAsync(Stream data, BlobMetadata? metadata = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(data);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync(ct).ConfigureAwait(false);
            var registry = await ReadRegistryAsync(ct).ConfigureAwait(false);
            if (registry.Count >= _options.MaximumBlobs) throw new IOException("Blob count capacity reached.");
            var remaining = _options.MaximumTotalBytes - registry.Values.Sum(e => e.Length);
            var blobId = Guid.NewGuid().ToString("N");
            var path = GetBlobPath(blobId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = Path.Combine(_baseDirectory, ".blob-temp-" + blobId);
            try {
                long length = 0;
                string digest;
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous | FileOptions.WriteThrough)) {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = ArrayPool<byte>.Shared.Rent(81920);
                    try {
                        int read;
                        while ((read = await data.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) != 0) {
                            length = checked(length + read);
                            if (length > _options.MaximumBlobBytes || length > remaining)
                                throw new IOException("Blob byte capacity reached.");
                            hash.AppendData(buffer, 0, read);
                            await stream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        }
                        digest = Convert.ToHexStringLower(hash.GetHashAndReset());
                    } finally { ArrayPool<byte>.Shared.Return(buffer); }
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }
                ct.ThrowIfCancellationRequested();
                _options.BeforePublish?.Invoke(temp, path);
                PersistentFile.CheckPath(path);
                File.Move(temp, path);
                PersistentFile.SyncDirectory(Path.GetDirectoryName(path)!);
                PersistentFile.SyncDirectory(_baseDirectory);
                var effective = (metadata ?? new BlobMetadata()) with {
                    ContentLength = metadata?.ContentLength ?? length,
                    ContentType = metadata?.ContentType ?? DetectContentType(path),
                    Tags = metadata?.Tags.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) ?? new Dictionary<string, string>()
                };
                registry[blobId] = new RegistryEntry(effective, digest, length, _options.TimeProvider.GetUtcNow());
                await WriteRegistryAsync(registry, ct).ConfigureAwait(false);
                return blobId;
            } finally {
                if (File.Exists(temp)) File.Delete(temp);
                DeleteEmptyPrefix(path);
            }
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask<Stream> OpenReadAsync(string blobId, CancellationToken ct = default) {
        var path = GetBlobPath(blobId);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync(ct).ConfigureAwait(false);
            var registry = await ReadRegistryAsync(ct).ConfigureAwait(false);
            if (!registry.TryGetValue(blobId, out var entry)) throw new FileNotFoundException("Blob is not registered.", path);
            FileStream stream;
            try {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            } catch (FileNotFoundException) {
                registry.Remove(blobId);
                await WriteRegistryAsync(registry, ct).ConfigureAwait(false);
                throw;
            }
            try {
                if (stream.Length != entry.Length || Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)) != entry.Sha256) {
                    registry.Remove(blobId);
                    await WriteRegistryAsync(registry, ct).ConfigureAwait(false);
                    throw new InvalidDataException($"Blob integrity verification failed: {blobId}");
                }
                stream.Position = 0;
                return stream;
            } catch {
                await stream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask<BlobMetadata?> GetMetadataAsync(string blobId, CancellationToken ct = default) {
        GetBlobPath(blobId);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync(ct).ConfigureAwait(false);
            var registry = await ReadRegistryAsync(ct).ConfigureAwait(false);
            return registry.TryGetValue(blobId, out var entry) ? entry.Metadata : null;
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ExistsAsync(string blobId, CancellationToken ct = default) {
        try {
            await using var stream = await OpenReadAsync(blobId, ct).ConfigureAwait(false);
            return true;
        } catch (FileNotFoundException) { return false; }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(string blobId, CancellationToken ct = default) {
        var path = GetBlobPath(blobId);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync(ct).ConfigureAwait(false);
            var registry = await ReadRegistryAsync(ct).ConfigureAwait(false);
            if (!registry.ContainsKey(blobId)) return false;
            EnsureNotReading(path);
            registry.Remove(blobId);
            await WriteRegistryAsync(registry, ct).ConfigureAwait(false);
            File.Delete(path);
            DeleteEmptyPrefix(path);
            return true;
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask ClearAsync(CancellationToken ct = default) {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync(ct).ConfigureAwait(false);
            var registry = await ReadRegistryAsync(ct).ConfigureAwait(false);
            var paths = registry.Keys.Select(GetBlobPath).ToArray();
            foreach (var path in paths) EnsureNotReading(path);
            PersistentFile.CheckPath(_registryPath);
            File.Delete(_registryPath);
            PersistentFile.SyncDirectory(_baseDirectory);
            foreach (var path in paths) {
                File.Delete(path);
                DeleteEmptyPrefix(path);
            }
        } finally { _lock.Release(); }
    }

    /// <summary>Collects expired registered blobs and abandoned files not in a complete caller-supplied live set.
    /// The caller must prevent new references to collectible IDs while this operation runs.</summary>
    public async Task<int> CollectGarbageAsync(IReadOnlySet<string> liveBlobIds, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(liveBlobIds);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            ThrowIfDisposed();
            using var lease = await AcquireAsync(ct).ConfigureAwait(false);
            var registry = await ReadRegistryAsync(ct).ConfigureAwait(false);
            var cutoff = _options.TimeProvider.GetUtcNow() - _options.Retention;
            var paths = new List<string>();
            foreach (var directory in Directory.EnumerateDirectories(_baseDirectory)) {
                PersistentFile.CheckPath(directory);
                if (Path.GetFileName(directory).Length != 2) continue;
                foreach (var path in Directory.EnumerateFiles(directory)) {
                    var id = Path.GetFileName(path);
                    if (!IsGeneratedId(id) || liveBlobIds.Contains(id) || GetBlobPath(id) != path) continue;
                    var created = registry.TryGetValue(id, out var entry) ? entry.CreatedAt : File.GetLastWriteTimeUtc(path);
                    if (created > cutoff) continue;
                    EnsureNotReading(path);
                    paths.Add(path);
                }
            }
            foreach (var path in paths) registry.Remove(Path.GetFileName(path));
            await WriteRegistryAsync(registry, ct).ConfigureAwait(false);
            foreach (var path in paths) {
                File.Delete(path);
                DeleteEmptyPrefix(path);
            }
            foreach (var temp in Directory.EnumerateFiles(_baseDirectory, ".blob-temp-*")) {
                PersistentFile.CheckPath(temp);
                if (File.GetLastWriteTimeUtc(temp) > cutoff) continue;
                File.Delete(temp);
            }
            return paths.Count;
        } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public void Dispose() {
        _lock.Wait();
        try { _disposed = true; } finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private ValueTask<FileStream> AcquireAsync(CancellationToken ct) =>
        PersistentFile.AcquireAsync(Path.Combine(_baseDirectory, ".storage.lock"), _options.LockTimeout, ct);

    private static bool IsGeneratedId(string id) => id.Length == 32 && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private string GetBlobPath(string blobId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);
        if (blobId.Length > 128 || blobId is "." or ".." || blobId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Blob IDs must be flat ASCII identifiers.", nameof(blobId));
        var path = Path.Combine(_baseDirectory, blobId[..Math.Min(2, blobId.Length)], blobId);
        PersistentFile.CheckPath(path);
        return path;
    }

    private static void EnsureNotReading(string path) {
        PersistentFile.CheckPath(path);
        if (!File.Exists(path)) return;
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
    }

    private static void DeleteEmptyPrefix(string path) {
        var prefix = Path.GetDirectoryName(path)!;
        PersistentFile.CheckPath(prefix);
        if (!Directory.Exists(prefix)) return;
        if (!Directory.EnumerateFileSystemEntries(prefix).Any()) {
            Directory.Delete(prefix);
            PersistentFile.SyncDirectory(Path.GetDirectoryName(prefix)!);
        } else {
            PersistentFile.SyncDirectory(prefix);
        }
    }

    private async Task<Dictionary<string, RegistryEntry>> ReadRegistryAsync(CancellationToken ct) {
        PersistentFile.CheckPath(_registryPath);
        PersistentFile.RecoverTemps(_registryPath);
        if (!File.Exists(_registryPath)) return new(StringComparer.Ordinal);
        if (new FileInfo(_registryPath).Length > _options.MaximumRegistryBytes) throw new InvalidDataException("Blob registry exceeds configured size.");
        try {
            var registry = JsonSerializer.Deserialize<Dictionary<string, RegistryEntry>>(
                await File.ReadAllBytesAsync(_registryPath, ct).ConfigureAwait(false)) ?? throw new JsonException("Null registry.");
            foreach (var pair in registry) {
                GetBlobPath(pair.Key);
                if (pair.Value is null || pair.Value.Length < 0 || pair.Value.Sha256 is not { Length: 64 } || pair.Value.Metadata is null)
                    throw new JsonException("Invalid blob registry entry.");
            }
            return registry;
        } catch (JsonException ex) {
            throw new InvalidDataException("Blob registry is corrupt; explicit recovery is required.", ex);
        }
    }

    private Task WriteRegistryAsync(Dictionary<string, RegistryEntry> registry, CancellationToken ct) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(registry);
        if (bytes.LongLength > _options.MaximumRegistryBytes) throw new IOException("Blob registry capacity reached.");
        return PersistentFile.WriteAsync(_registryPath, bytes, _options.BeforePublish, ct);
    }

    private static string? DetectContentType(string path) {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        var h = header[..stream.Read(header)];
        if (h.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47 })) return "image/png";
        if (h.StartsWith(new byte[] { 0xff, 0xd8, 0xff })) return "image/jpeg";
        if (h.StartsWith("GIF8"u8)) return "image/gif";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (h.StartsWith("%PDF"u8)) return "application/pdf";
        if (h.StartsWith(new byte[] { 0x1f, 0x8b })) return "application/gzip";
        if (h.StartsWith(new byte[] { 0x50, 0x4b, 0x03, 0x04 })) return "application/zip";
        return null;
    }

    private sealed record RegistryEntry(BlobMetadata Metadata, string Sha256, long Length, DateTimeOffset CreatedAt);
}
