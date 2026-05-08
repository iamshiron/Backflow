using Shiron.Lib.Pipeline.Caching;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public abstract class BlobStoreTests {
    protected abstract IBlobStore CreateStore();

    [Fact]
    public async Task StoreAsync_ReturnsContentHash() {
        var store = CreateStore();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var hash = await store.StoreAsync(data);
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public async Task StoreAsync_WithProvidedHash_UsesProvidedHash() {
        var store = CreateStore();
        var data = new byte[] { 1, 2, 3 };
        var expectedHash = "abc123";
        var hash = await store.StoreAsync(data, expectedHash);
        Assert.Equal(expectedHash, hash);
    }

    [Fact]
    public async Task StoreAsync_SameData_IsIdempotent() {
        var store = CreateStore();
        var data = new byte[] { 1, 2, 3 };
        var hash1 = await store.StoreAsync(data);
        var hash2 = await store.StoreAsync(data);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task RetrieveAsync_AfterStore_ReturnsSameData() {
        var store = CreateStore();
        var data = new byte[] { 10, 20, 30, 40, 50 };
        var hash = await store.StoreAsync(data);
        var retrieved = await store.RetrieveAsync(hash);
        Assert.Equal(data, retrieved);
    }

    [Fact]
    public async Task RetrieveAsync_NonExistent_ReturnsNull() {
        var store = CreateStore();
        var retrieved = await store.RetrieveAsync("nonexistent");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ExistsAsync_AfterStore_ReturnsTrue() {
        var store = CreateStore();
        var hash = await store.StoreAsync([1, 2, 3]);
        Assert.True(await store.ExistsAsync(hash));
    }

    [Fact]
    public async Task ExistsAsync_NonExistent_ReturnsFalse() {
        var store = CreateStore();
        Assert.False(await store.ExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesBlob() {
        var store = CreateStore();
        var hash = await store.StoreAsync([1, 2, 3]);
        Assert.True(await store.ExistsAsync(hash));

        await store.DeleteAsync(hash);
        Assert.False(await store.ExistsAsync(hash));
        Assert.Null(await store.RetrieveAsync(hash));
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNotThrow() {
        var store = CreateStore();
        await store.DeleteAsync("nonexistent");
    }

    [Fact]
    public async Task StoreAsync_LargeData_RetrievesCorrectly() {
        var store = CreateStore();
        var data = new byte[1024 * 1024];
        new Random(42).NextBytes(data);
        var hash = await store.StoreAsync(data);
        var retrieved = await store.RetrieveAsync(hash);
        Assert.Equal(data, retrieved);
    }

    public sealed class MemoryTests : BlobStoreTests {
        protected override IBlobStore CreateStore() => new MemoryBlobStore();
    }

    public sealed class FileSystemTests : BlobStoreTests, IAsyncLifetime {
        private string _tmpDir = null!;

        protected override IBlobStore CreateStore() => new FileSystemBlobStore(_tmpDir);

        public Task InitializeAsync() {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");
            return Task.CompletedTask;
        }

        public Task DisposeAsync() {
            if (Directory.Exists(_tmpDir)) {
                Directory.Delete(_tmpDir, true);
            }
            return Task.CompletedTask;
        }
    }
}
