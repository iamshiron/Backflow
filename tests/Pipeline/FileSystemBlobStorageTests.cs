using Shiron.Lib.Pipeline.BlobStorage;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class FileSystemBlobStorageStoreTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task StoreAsync_ReturnsNonEmptyBlobId() {
        using var storage = new FileSystemBlobStorage("test", TempDir());
        var data = new MemoryStream([1, 2, 3, 4]);

        var blobId = await storage.StoreAsync(data);

        Assert.False(string.IsNullOrEmpty(blobId));
    }

    [Fact]
    public async Task StoreAsync_CreatesFileInPrefixedFolder() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var data = new MemoryStream([1, 2, 3]);

        var blobId = await storage.StoreAsync(data);

        var prefix = blobId[..2];
        Assert.True(File.Exists(Path.Combine(dir, prefix, blobId)));
    }

    [Fact]
    public async Task StoreAsync_CreatesRegistryJson() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        await storage.StoreAsync(new MemoryStream([1, 2, 3]));

        Assert.True(File.Exists(Path.Combine(dir, "registry.json")));
    }

    [Fact]
    public async Task StoreAsync_WithMetadata_WritesMetadataToRegistry() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var meta = new BlobMetadata { ContentType = "text/plain" };

        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]), meta);
        var storedMeta = await storage.GetMetadataAsync(blobId);

        Assert.NotNull(storedMeta);
        Assert.Equal("text/plain", storedMeta!.ContentType);
    }

    [Fact]
    public async Task StoreAsync_WithMetadata_FillsContentLengthWhenNotProvided() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var meta = new BlobMetadata { ContentType = "application/octet-stream" };

        var blobId = await storage.StoreAsync(new MemoryStream(bytes), meta);
        var storedMeta = await storage.GetMetadataAsync(blobId);

        Assert.Equal(bytes.Length, storedMeta!.ContentLength);
    }

    [Fact]
    public async Task StoreAsync_WithMetadata_PreservesExplicitContentLength() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var meta = new BlobMetadata { ContentLength = 999 };

        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]), meta);
        var storedMeta = await storage.GetMetadataAsync(blobId);

        Assert.Equal(999, storedMeta!.ContentLength);
    }

    [Fact]
    public async Task StoreAsync_WithoutMetadata_StillWritesContentLengthToRegistry() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var bytes = new byte[] { 10, 20, 30 };

        var blobId = await storage.StoreAsync(new MemoryStream(bytes));
        var storedMeta = await storage.GetMetadataAsync(blobId);

        Assert.NotNull(storedMeta);
        Assert.Equal(bytes.Length, storedMeta!.ContentLength);
    }

    [Fact]
    public async Task StoreAsync_WithTags_PersistsTagsInRegistry() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var meta = new BlobMetadata {
            ContentType = "image/png",
            Tags = new Dictionary<string, string> {
                ["width"] = "3840",
                ["height"] = "2160"
            }
        };

        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]), meta);
        var result = await storage.GetMetadataAsync(blobId);

        Assert.Equal(2, result!.Tags.Count);
        Assert.Equal("3840", result.Tags["width"]);
        Assert.Equal("2160", result.Tags["height"]);
    }

    [Fact]
    public async Task StoreAsync_MetadataPersistsAcrossStorageInstances() {
        var dir = TempDir();
        string blobId;

        using (var storage = new FileSystemBlobStorage("test", dir)) {
            var meta = new BlobMetadata {
                ContentType = "image/jpeg",
                Tags = new Dictionary<string, string> { ["source"] = "camera" }
            };
            blobId = await storage.StoreAsync(new MemoryStream([4, 5, 6]), meta);
        }

        using (var storage2 = new FileSystemBlobStorage("test", dir)) {
            var result = await storage2.GetMetadataAsync(blobId);

            Assert.NotNull(result);
            Assert.Equal("image/jpeg", result!.ContentType);
            Assert.Equal(3, result.ContentLength);
            Assert.Equal("camera", result.Tags["source"]);
        }
    }

    [Fact]
    public async Task StoreAsync_RegistryJsonContainsValidStructure() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var meta = new BlobMetadata { ContentType = "text/plain" };
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]), meta);

        var json = await File.ReadAllTextAsync(Path.Combine(dir, "registry.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty(blobId, out var entry));
        Assert.True(entry.TryGetProperty("Metadata", out var metadata));
        Assert.True(metadata.TryGetProperty("ContentType", out var ct));
        Assert.Equal("text/plain", ct.GetString());
    }
}

public class FileSystemBlobStorageOpenReadTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task OpenReadAsync_ReturnsOriginalData() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var original = new byte[] { 10, 20, 30, 40 };
        var blobId = await storage.StoreAsync(new MemoryStream(original));

        var stream = await storage.OpenReadAsync(blobId);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        stream.Dispose();

        Assert.Equal(original, ms.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_MissingBlob_ThrowsFileNotFound() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            storage.OpenReadAsync("nonexistent").AsTask());
    }
}

public class FileSystemBlobStorageGetMetadataTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetMetadataAsync_WithMetadata_ReturnsMetadata() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var meta = new BlobMetadata {
            ContentType = "image/png",
            Tags = new Dictionary<string, string> { ["source"] = "test" }
        };
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]), meta);

        var result = await storage.GetMetadataAsync(blobId);

        Assert.NotNull(result);
        Assert.Equal("image/png", result!.ContentType);
        Assert.Equal("test", result.Tags["source"]);
    }

    [Fact]
    public async Task GetMetadataAsync_WithoutMetadata_ReturnsAutoGeneratedMetadata() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]));

        var result = await storage.GetMetadataAsync(blobId);

        Assert.NotNull(result);
        Assert.Equal(3, result!.ContentLength);
    }
}

public class FileSystemBlobStorageExistsTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExistsAsync_StoredBlob_ReturnsTrue() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]));

        Assert.True(await storage.ExistsAsync(blobId));
    }

    [Fact]
    public async Task ExistsAsync_MissingBlob_ReturnsFalse() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);

        Assert.False(await storage.ExistsAsync("does-not-exist"));
    }
}

public class FileSystemBlobStorageRemoveTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task RemoveAsync_ExistingBlob_ReturnsTrueAndDeletesFile() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]));

        var removed = await storage.RemoveAsync(blobId);

        Assert.True(removed);
        Assert.False(await storage.ExistsAsync(blobId));
    }

    [Fact]
    public async Task RemoveAsync_NonExistentBlob_ReturnsFalse() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);

        Assert.False(await storage.RemoveAsync("ghost"));
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromRegistry() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]));

        await storage.RemoveAsync(blobId);
        var meta = await storage.GetMetadataAsync(blobId);

        Assert.Null(meta);
    }

    [Fact]
    public async Task RemoveAsync_CleansUpEmptyPrefixFolder() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var blobId = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        var prefix = blobId[..2];

        await storage.RemoveAsync(blobId);

        Assert.False(Directory.Exists(Path.Combine(dir, prefix)));
    }
}

public class FileSystemBlobStorageClearTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task ClearAsync_DeletesAllBlobsAndRegistry() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        await storage.StoreAsync(new MemoryStream([1]));
        await storage.StoreAsync(new MemoryStream([2]));
        await storage.StoreAsync(new MemoryStream([3]));

        await storage.ClearAsync();

        Assert.Empty(Directory.GetDirectories(dir));
        Assert.False(File.Exists(Path.Combine(dir, "registry.json")));
    }

    [Fact]
    public async Task ClearAsync_EmptyDir_DoesNotThrow() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);

        await storage.ClearAsync();

        Assert.Empty(Directory.GetDirectories(dir));
    }
}

public class FileSystemBlobStorageConstructorTests {
    [Fact]
    public void Constructor_CreatesDirectoryIfMissing() {
        var dir = Path.Combine(Path.GetTempPath(), $"blob-test-new-{Guid.NewGuid():N}");
        try {
            using var storage = new FileSystemBlobStorage("test", dir);
            Assert.True(Directory.Exists(dir));
        } finally {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Constructor_NullName_Throws() {
        Assert.Throws<ArgumentNullException>(() => new FileSystemBlobStorage(null!, "/tmp/x"));
    }

    [Fact]
    public void Constructor_NullBaseDirectory_Throws() {
        Assert.Throws<ArgumentNullException>(() => new FileSystemBlobStorage("test", null!));
    }

    [Fact]
    public void Name_ReturnsConstructorValue() {
        using var storage = new FileSystemBlobStorage("my-storage", TempDir());
        Assert.Equal("my-storage", storage.Name);
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");
}

public class FileSystemBlobStorageRoundTripTests {
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"blob-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task StoreThenRead_LargeData_PreservesContent() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);
        var original = new byte[64 * 1024];
        Random.Shared.NextBytes(original);

        var blobId = await storage.StoreAsync(new MemoryStream(original));
        var stream = await storage.OpenReadAsync(blobId);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        stream.Dispose();

        Assert.Equal(original, ms.ToArray());
    }

    [Fact]
    public async Task StoreMultiple_ReadEach_CorrectData() {
        var dir = TempDir();
        using var storage = new FileSystemBlobStorage("test", dir);

        var id1 = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        var id2 = await storage.StoreAsync(new MemoryStream([4, 5, 6]));

        Assert.NotEqual(id1, id2);

        var s1 = await storage.OpenReadAsync(id1);
        using var m1 = new MemoryStream();
        await s1.CopyToAsync(m1);
        s1.Dispose();

        var s2 = await storage.OpenReadAsync(id2);
        using var m2 = new MemoryStream();
        await s2.CopyToAsync(m2);
        s2.Dispose();

        Assert.Equal([1, 2, 3], m1.ToArray());
        Assert.Equal([4, 5, 6], m2.ToArray());
    }
}
