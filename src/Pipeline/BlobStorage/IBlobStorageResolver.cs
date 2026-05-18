namespace Shiron.Lib.Pipeline.BlobStorage;

public interface IBlobStorageResolver {
    IBlobStorage Resolve(BlobMetadata? metadata);
    IBlobStorage ResolveByName(string storageName);
}
