using Shiron.Backflow.BlobStorage;
using Xunit;

namespace Shiron.Backflow.Tests;

// Integration tests: persistence, OS sharing, and symlink behavior require the actual filesystem.
public sealed class FileSystemBlobStorageDurabilityTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "backflow-blobs-" + Guid.NewGuid().ToString("N"));
    private string BlobPath(string id) => Path.Combine(_directory, id[..2], id);

    [Fact]
    public async Task StoreAsync_ConcurrentInstances_PreservesAllRegistryEntries() {
        using var first = new FileSystemBlobStorage("disk", _directory);
        using var second = new FileSystemBlobStorage("disk", _directory);
        var ids = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
            await (i % 2 == 0 ? first : second).StoreAsync(new MemoryStream([(byte) i]))));
        using var reader = new FileSystemBlobStorage("disk", _directory);
        foreach (var id in ids) Assert.True(await reader.ExistsAsync(id));
    }

    [Fact]
    public async Task StoreAsync_IdenticalBytes_UsesIndependentOwnershipWithoutSharedDelete() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        var first = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        var second = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        Assert.NotEqual(first, second);
        Assert.True(await storage.RemoveAsync(first));
        Assert.True(await storage.ExistsAsync(second));
    }

    [Fact]
    public async Task OpenReadAsync_CorruptBytes_InvalidatesAndThrowsNeverReturnsData() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        var id = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        await File.WriteAllBytesAsync(BlobPath(id), [1, 2, 4]);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.OpenReadAsync(id).AsTask());
        Assert.False(await storage.ExistsAsync(id));
        Assert.Null(await storage.GetMetadataAsync(id));
    }

    [Fact]
    public async Task OpenReadAsync_MissingData_InvalidatesRegistration() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        var id = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        File.Delete(BlobPath(id));
        Assert.False(await storage.ExistsAsync(id));
        Assert.Null(await storage.GetMetadataAsync(id));
    }

    [Fact]
    public async Task RemoveAsync_ActiveReader_RejectsDeletionUntilReaderDisposed() {
        using var writer = new FileSystemBlobStorage("disk", _directory);
        using var remover = new FileSystemBlobStorage("disk", _directory);
        var id = await writer.StoreAsync(new MemoryStream([1, 2, 3]));
        var read = await writer.OpenReadAsync(id);
        try {
            await Assert.ThrowsAsync<IOException>(() => remover.RemoveAsync(id).AsTask());
            Assert.Equal(1, read.ReadByte());
            Assert.NotNull(await remover.GetMetadataAsync(id));
        } finally { await read.DisposeAsync(); }
        Assert.True(await remover.RemoveAsync(id));
    }

    [Fact]
    public async Task StoreAsync_InputFailure_DoesNotPublishPartialBlob() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync(new FailingStream()).AsTask());
        Assert.DoesNotContain(Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories), p => Path.GetFileName(p) != ".storage.lock");
    }

    [Fact]
    public async Task StoreAsync_RegistryPublishFailure_PreservesPreviouslyPublishedBlobAndCollectsOrphan() {
        string existing;
        using (var initial = new FileSystemBlobStorage("disk", _directory))
            existing = await initial.StoreAsync(new MemoryStream([1]));
        var previous = await File.ReadAllBytesAsync(Path.Combine(_directory, "registry.json"));
        using (var failing = new FileSystemBlobStorage("disk", _directory, new FileSystemBlobStorageOptions {
            BeforePublish = (_, target) => {
                if (Path.GetFileName(target) == "registry.json") throw new IOException("Injected registry publication failure.");
            }
        })) {
            await Assert.ThrowsAsync<IOException>(() => failing.StoreAsync(new MemoryStream([2])).AsTask());
            Assert.Equal(previous, await File.ReadAllBytesAsync(Path.Combine(_directory, "registry.json")));
            Assert.True(await failing.ExistsAsync(existing));
        }
        using var recovery = new FileSystemBlobStorage("disk", _directory, new FileSystemBlobStorageOptions { Retention = TimeSpan.Zero });
        Assert.Equal(1, await recovery.CollectGarbageAsync(new HashSet<string> { existing }));
        Assert.True(await recovery.ExistsAsync(existing));
    }

    [Fact]
    public async Task StoreAsync_CapacityReached_RejectsAdmissionRatherThanEvictingLiveBlob() {
        using var storage = new FileSystemBlobStorage("disk", _directory, new FileSystemBlobStorageOptions {
            MaximumBlobs = 1, MaximumBlobBytes = 3, MaximumTotalBytes = 3
        });
        await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync(new MemoryStream([1, 2, 3, 4])).AsTask());
        var id = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync(new MemoryStream([4])).AsTask());
        Assert.True(await storage.ExistsAsync(id));
    }

    [Fact]
    public async Task CollectGarbageAsync_ExpiredButLiveBlob_PreservesLiveSetAndDeletesUnreferenced() {
        using var storage = new FileSystemBlobStorage("disk", _directory, new FileSystemBlobStorageOptions { Retention = TimeSpan.Zero });
        var live = await storage.StoreAsync(new MemoryStream([1]));
        var dead = await storage.StoreAsync(new MemoryStream([2]));
        Assert.Equal(1, await storage.CollectGarbageAsync(new HashSet<string> { live }));
        Assert.True(await storage.ExistsAsync(live));
        Assert.False(await storage.ExistsAsync(dead));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/tmp/outside")]
    [InlineData("..\\outside")]
    [InlineData("%2e%2e%2foutside")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task OpenReadAsync_PathTraversal_RejectsBeforeAccess(string id) {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(id).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.RemoveAsync(id).AsTask());
    }

    [Fact]
    public async Task OpenReadAsync_SymlinkReplacement_RejectsOutsideFile() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        var id = await storage.StoreAsync(new MemoryStream([1]));
        var outside = Path.Combine(_directory, "outside");
        await File.WriteAllBytesAsync(outside, [1]);
        File.Delete(BlobPath(id));
        File.CreateSymbolicLink(BlobPath(id), outside);
        await Assert.ThrowsAsync<IOException>(() => storage.OpenReadAsync(id).AsTask());
        await Assert.ThrowsAsync<IOException>(() => storage.ClearAsync().AsTask());
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task ClearAsync_UnrelatedDirectories_DoesNotRecursivelyDeleteUserData() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        await storage.StoreAsync(new MemoryStream([1]));
        var unrelated = Path.Combine(_directory, "unrelated");
        Directory.CreateDirectory(unrelated);
        var path = Path.Combine(unrelated, "important.txt");
        await File.WriteAllTextAsync(path, "keep");
        await storage.ClearAsync();
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task GetMetadataAsync_CorruptRegistry_ThrowsInsteadOfSilentlyDiscardingData() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        var id = await storage.StoreAsync(new MemoryStream([1]));
        await File.WriteAllTextAsync(Path.Combine(_directory, "registry.json"), "{");
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.GetMetadataAsync(id).AsTask());
        Assert.True(File.Exists(BlobPath(id)));
    }

    [Fact]
    public async Task StoreAsync_Canceled_DoesNotPublish() {
        using var storage = new FileSystemBlobStorage("disk", _directory);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(new MemoryStream([1]), ct: canceled.Token).AsTask());
        Assert.False(File.Exists(Path.Combine(_directory, "registry.json")));
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class FailingStream : MemoryStream {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Injected input failure."));
    }
}
