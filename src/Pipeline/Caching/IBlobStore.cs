namespace Shiron.Lib.Pipeline.Caching;

public interface IBlobStore : IAsyncDisposable, IDisposable {
    ValueTask<string> StoreAsync(byte[] data, string? contentHash = null, CancellationToken ct = default);
    ValueTask<byte[]?> RetrieveAsync(string contentHash, CancellationToken ct = default);
    ValueTask<bool> ExistsAsync(string contentHash, CancellationToken ct = default);
    ValueTask DeleteAsync(string contentHash, CancellationToken ct = default);
}
