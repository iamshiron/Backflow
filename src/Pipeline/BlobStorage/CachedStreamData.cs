using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.BlobStorage;

public sealed class CachedStreamData : IStreamData {
    private readonly BlobReference _reference;
    private readonly IBlobStorageResolver _resolver;

    public BlobReference Reference => _reference;

    public CachedStreamData(BlobReference reference, IBlobStorageResolver resolver) {
        _reference = reference;
        _resolver = resolver;
    }

    public Stream OpenRead() {
        var storage = _resolver.ResolveByName(_reference.StorageName);
        return storage.OpenReadAsync(_reference.BlobId).GetAwaiter().GetResult();
    }

    public void Dispose() { }
}
