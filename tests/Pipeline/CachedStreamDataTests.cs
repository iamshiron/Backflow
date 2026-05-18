using Shiron.Lib.Pipeline.BlobStorage;
using Shiron.Lib.Pipeline.Types;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

file class TestBlobStorageRegistry : BlobStorageRegistry {
    public TestBlobStorageRegistry(params IBlobStorage[] storages) {
        foreach (var s in storages) Register(s);
    }
}

public class CachedStreamDataOpenReadTests {
    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-csd-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public async Task OpenRead_ReturnsStoredContent() {
        using var storage = MakeStorage("test");
        var registry = new TestBlobStorageRegistry(storage);

        var original = new byte[] { 7, 8, 9 };
        var blobId = await storage.StoreAsync(new MemoryStream(original));
        var reference = new BlobReference("test", blobId);

        var cached = new CachedStreamData(reference, registry);
        using var stream = cached.OpenRead();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        Assert.Equal(original, ms.ToArray());
    }

    [Fact]
    public async Task OpenRead_CanBeCalledMultipleTimes() {
        using var storage = MakeStorage("test");
        var registry = new TestBlobStorageRegistry(storage);

        var original = new byte[] { 1, 2, 3 };
        var blobId = await storage.StoreAsync(new MemoryStream(original));
        var reference = new BlobReference("test", blobId);

        var cached = new CachedStreamData(reference, registry);

        using var s1 = cached.OpenRead();
        using var ms1 = new MemoryStream();
        s1.CopyTo(ms1);

        using var s2 = cached.OpenRead();
        using var ms2 = new MemoryStream();
        s2.CopyTo(ms2);

        Assert.Equal(ms1.ToArray(), ms2.ToArray());
    }
}

public class CachedStreamDataPropertyTests {
    [Fact]
    public void Reference_ReturnsConstructorValue() {
        var reference = new BlobReference("disk", "abc");
        var cached = new CachedStreamData(reference, new BlobStorageRegistry());

        Assert.Equal(reference, cached.Reference);
    }
}

public class CachedStreamDataDisposeTests {
    [Fact]
    public void Dispose_DoesNotThrow() {
        var reference = new BlobReference("disk", "abc");
        var cached = new CachedStreamData(reference, new BlobStorageRegistry());
        cached.Dispose();
    }
}
