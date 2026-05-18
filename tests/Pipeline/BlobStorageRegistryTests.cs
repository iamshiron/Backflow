using Shiron.Lib.Pipeline.BlobStorage;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

file class TestBlobStorageRegistry : BlobStorageRegistry {
    public TestBlobStorageRegistry() { }

    public TestBlobStorageRegistry(params IBlobStorage[] storages) {
        foreach (var s in storages) Register(s);
    }
}

public class BlobStorageRegistryRegisterTests {
    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public void Register_NullStorage_Throws() {
        Assert.Throws<ArgumentNullException>(() => new NullRegisteringRegistry());
    }

    [Fact]
    public void ResolveByName_RegisteredStorage_ReturnsStorage() {
        using var storage = MakeStorage("disk");
        var registry = new TestBlobStorageRegistry(storage);

        var result = registry.ResolveByName("disk");

        Assert.Same(storage, result);
    }
}

file class NullRegisteringRegistry : BlobStorageRegistry {
    public NullRegisteringRegistry() => Register(null!);
}

public class BlobStorageRegistryResolveTests {
    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public void Resolve_NoStorages_Throws() {
        var registry = new TestBlobStorageRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
    }

    [Fact]
    public void Resolve_Default_ReturnsFirstRegistered() {
        using var s1 = MakeStorage("first");
        using var s2 = MakeStorage("second");
        var registry = new TestBlobStorageRegistry(s1, s2);

        var result = registry.Resolve(null);

        Assert.Same(s1, result);
    }

    [Fact]
    public void Resolve_SingleStorage_ReturnsIt() {
        using var storage = MakeStorage("only");
        var registry = new TestBlobStorageRegistry(storage);

        Assert.Same(storage, registry.Resolve(null));
    }
}

public class BlobStorageRegistryResolveByNameTests {
    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public void ResolveByName_MissingName_Throws() {
        var registry = new TestBlobStorageRegistry();
        Assert.Throws<KeyNotFoundException>(() => registry.ResolveByName("missing"));
    }

    [Fact]
    public void ResolveByName_MultipleStorages_ReturnsCorrectOne() {
        using var s1 = MakeStorage("a");
        using var s2 = MakeStorage("b");
        var registry = new TestBlobStorageRegistry(s1, s2);

        Assert.Same(s2, registry.ResolveByName("b"));
        Assert.Same(s1, registry.ResolveByName("a"));
    }
}

public class BlobStorageRegistryDisposeTests {
    [Fact]
    public void Dispose_ClearsStorages() {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        var registry = new TestBlobStorageRegistry(new FileSystemBlobStorage("test", dir));

        registry.Dispose();

        Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
    }
}

public class BlobStorageRegistryOverrideTests {
    private class RoutingRegistry : BlobStorageRegistry {
        private readonly IBlobStorage _small;
        private readonly IBlobStorage _large;

        public RoutingRegistry(IBlobStorage small, IBlobStorage large) {
            Register(small);
            Register(large);
            _small = small;
            _large = large;
        }

        public override IBlobStorage Resolve(BlobMetadata? metadata) {
            if (metadata is { ContentLength: < 100 })
                return _small;
            return _large;
        }
    }

    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public async Task Resolve_Override_RoutesBySize() {
        using var small = MakeStorage("small");
        using var large = MakeStorage("large");

        var registry = new RoutingRegistry(small, large);

        var smallId = await registry.Resolve(new BlobMetadata { ContentLength = 50 })
            .StoreAsync(new MemoryStream([1, 2, 3]));
        var largeId = await registry.Resolve(new BlobMetadata { ContentLength = 200 })
            .StoreAsync(new MemoryStream([4, 5, 6]));

        Assert.True(await small.ExistsAsync(smallId));
        Assert.True(await large.ExistsAsync(largeId));
        Assert.False(await large.ExistsAsync(smallId));
        Assert.False(await small.ExistsAsync(largeId));
    }
}
