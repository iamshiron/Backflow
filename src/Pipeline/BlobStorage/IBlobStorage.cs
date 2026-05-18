namespace Shiron.Lib.Pipeline.BlobStorage;

public interface IBlobStorage : IAsyncDisposable, IDisposable {
    string Name { get; }

    ValueTask<string> StoreAsync(Stream data, BlobMetadata? metadata = null, CancellationToken ct = default);
    ValueTask<Stream> OpenReadAsync(string blobId, CancellationToken ct = default);
    ValueTask<BlobMetadata?> GetMetadataAsync(string blobId, CancellationToken ct = default);
    ValueTask<bool> ExistsAsync(string blobId, CancellationToken ct = default);
    ValueTask<bool> RemoveAsync(string blobId, CancellationToken ct = default);
    ValueTask ClearAsync(CancellationToken ct = default);
}
