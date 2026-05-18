using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.BlobStorage;

public sealed class CachedBlob : IBlob {
    private readonly CachedStreamData _storage;

    public CachedBlob(CachedStreamData storage) {
        _storage = storage;
    }

    public IStreamData Storage => _storage;
    public BlobReference Reference => _storage.Reference;
}
