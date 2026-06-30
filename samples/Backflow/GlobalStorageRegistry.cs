using Shiron.Backflow.BlobStorage;

namespace Shiron.Backflow.Samples;

public class GlobalStorageRegistry : BlobStorageRegistry {
    public IBlobStorage Default { get; }

    public GlobalStorageRegistry(string basePath) {
        Default = new FileSystemBlobStorage("default", Path.Join(basePath, "default"));
        Register(Default);
    }

    public override IBlobStorage Resolve(BlobMetadata? metadata) {
        return Default;
    }
}
