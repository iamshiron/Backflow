namespace Shiron.Backflow.BlobStorage;

public interface IBlobStorageResolver {
    IBlobStorage Resolve(BlobMetadata? metadata);
    IBlobStorage ResolveByName(string storageName);
}
