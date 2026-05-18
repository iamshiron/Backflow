using Shiron.Lib.Pipeline.BlobStorage;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class BlobStorageRegistryRegisterTests {
    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public void Register_NullStorage_Throws() {
        var registry = new BlobStorageRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void ResolveByName_RegisteredStorage_ReturnsStorage() {
        using var storage = MakeStorage("disk");
        var registry = new BlobStorageRegistry();
        registry.Register(storage);

        var result = registry.ResolveByName("disk");

        Assert.Same(storage, result);
    }
}

public class BlobStorageRegistryResolveTests {
    private static FileSystemBlobStorage MakeStorage(string name) {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        return new FileSystemBlobStorage(name, dir);
    }

    [Fact]
    public void Resolve_NoStorages_Throws() {
        var registry = new BlobStorageRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
    }

    [Fact]
    public void Resolve_Default_ReturnsFirstRegistered() {
        using var s1 = MakeStorage("first");
        using var s2 = MakeStorage("second");
        var registry = new BlobStorageRegistry();
        registry.Register(s1);
        registry.Register(s2);

        var result = registry.Resolve(null);

        Assert.Same(s1, result);
    }

    [Fact]
    public void Resolve_SingleStorage_ReturnsIt() {
        using var storage = MakeStorage("only");
        var registry = new BlobStorageRegistry();
        registry.Register(storage);

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
        var registry = new BlobStorageRegistry();
        Assert.Throws<KeyNotFoundException>(() => registry.ResolveByName("missing"));
    }

    [Fact]
    public void ResolveByName_MultipleStorages_ReturnsCorrectOne() {
        using var s1 = MakeStorage("a");
        using var s2 = MakeStorage("b");
        var registry = new BlobStorageRegistry();
        registry.Register(s1);
        registry.Register(s2);

        Assert.Same(s2, registry.ResolveByName("b"));
        Assert.Same(s1, registry.ResolveByName("a"));
    }
}

public class BlobStorageRegistryDisposeTests {
    [Fact]
    public void Dispose_ClearsStorages() {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-reg-{Guid.NewGuid():N}");
        var registry = new BlobStorageRegistry();
        registry.Register(new FileSystemBlobStorage("test", dir));

        registry.Dispose();

        Assert.Throws<InvalidOperationException>(() => registry.Resolve(null));
    }
}

public class BlobStorageRegistryOverrideTests {
    private class RoutingRegistry : BlobStorageRegistry {
        private IBlobStorage? _small;
        private IBlobStorage? _large;

        public void SetTargets(IBlobStorage small, IBlobStorage large) {
            _small = small;
            _large = large;
        }

        public override IBlobStorage Resolve(BlobMetadata? metadata) {
            if (metadata is { ContentLength: < 100 } && _small is not null)
                return _small;
            return _large!;
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

        var registry = new RoutingRegistry();
        registry.Register(small);
        registry.Register(large);
        registry.SetTargets(small, large);

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
