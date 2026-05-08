using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class MemoryBlobStore : IBlobStore {
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public ValueTask<string> StoreAsync(byte[] data, string? contentHash = null, CancellationToken ct = default) {
        var hash = contentHash ?? ComputeHash(data);
        _blobs.TryAdd(hash, data);
        return new ValueTask<string>(hash);
    }

    public ValueTask<byte[]?> RetrieveAsync(string contentHash, CancellationToken ct = default) {
        return _blobs.TryGetValue(contentHash, out var data)
            ? new ValueTask<byte[]?>(data)
            : new ValueTask<byte[]?>((byte[]?) null);
    }

    public ValueTask<bool> ExistsAsync(string contentHash, CancellationToken ct = default) {
        return new ValueTask<bool>(_blobs.ContainsKey(contentHash));
    }

    public ValueTask DeleteAsync(string contentHash, CancellationToken ct = default) {
        _blobs.TryRemove(contentHash, out _);
        return ValueTask.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string ComputeHash(byte[] data) {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }
}
