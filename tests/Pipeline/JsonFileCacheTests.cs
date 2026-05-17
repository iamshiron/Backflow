using Shiron.Lib.Pipeline.Caching;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class JsonFileCacheTests {
    private static string TempCachePath() => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

    private static CacheKey MakeKey(string suffix = "") {
        return new CacheKey($"TestNode{suffix}", "1.0.0.0", $"inputHash{suffix}");
    }

    private static CacheEntry MakeEntry(string nodeTypeName = "TestNode", int outputValue = 42) {
        return new CacheEntry {
            NodeTypeName = nodeTypeName,
            Inputs = new Dictionary<string, CachePortValue> {
                ["in"] = new(outputValue, typeof(int).AssemblyQualifiedName ?? "System.Int32")
            },
            Outputs = new Dictionary<string, CachePortValue> {
                ["out"] = new(outputValue * 2, typeof(int).AssemblyQualifiedName ?? "System.Int32")
            }
        };
    }

    [Fact]
    public async Task SetAsync_WithoutFlush_TryGetReturnsFromPending() {
        using var cache = new JsonFileCache(TempCachePath());
        var key = MakeKey();
        var entry = MakeEntry();

        await cache.SetAsync(key, entry);
        var (found, result) = await cache.TryGetAsync(key);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(84, result!.Outputs["out"].Value);
    }

    [Fact]
    public async Task FlushAsync_PersistsToDisk() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
            await cache.FlushAsync();
        }

        Assert.True(File.Exists(path));
        var json = await File.ReadAllTextAsync(path);
        Assert.NotEmpty(json);
        Assert.Contains("TestNode", json);
    }

    [Fact]
    public async Task FlushAsync_PendingClearedAfterFlush() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
            await cache.FlushAsync();

            var content1 = await File.ReadAllTextAsync(path);

            await cache.FlushAsync();
            var content2 = await File.ReadAllTextAsync(path);

            Assert.Equal(content1, content2);
        }
    }

    [Fact]
    public async Task TryGetAsync_AfterFlushAndReopen_ReturnsEntry() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
            await cache.FlushAsync();
        }

        using (var cache2 = new JsonFileCache(path)) {
            var (found, result) = await cache2.TryGetAsync(key);

            Assert.True(found);
            Assert.NotNull(result);
            Assert.Equal("TestNode", result!.NodeTypeName);
            Assert.Equal(84, result.Outputs["out"].Value);
        }
    }

    [Fact]
    public async Task TryGetAsync_NotFound_ReturnsFalse() {
        using var cache = new JsonFileCache(TempCachePath());
        var key = MakeKey("nonexistent");

        var (found, result) = await cache.TryGetAsync(key);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAsync_PendingOverwritesFileStore() {
        var path = TempCachePath();
        var key = MakeKey();

        var entry1 = MakeEntry("Node1", 10);
        var entry2 = MakeEntry("Node2", 20);

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry1);
            await cache.FlushAsync();
        }

        using (var cache2 = new JsonFileCache(path)) {
            await cache2.SetAsync(key, entry2);
            var (found, result) = await cache2.TryGetAsync(key);

            Assert.True(found);
            Assert.Equal("Node2", result!.NodeTypeName);
            Assert.Equal(40, result.Outputs["out"].Value);
        }
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_ReturnsTrue() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
            await cache.FlushAsync();
        }

        using (var cache2 = new JsonFileCache(path)) {
            var removed = await cache2.RemoveAsync(key);
            Assert.True(removed);

            var (found, _) = await cache2.TryGetAsync(key);
            Assert.False(found);
        }
    }

    [Fact]
    public async Task RemoveAsync_NonExistentKey_ReturnsFalse() {
        using var cache = new JsonFileCache(TempCachePath());
        var key = MakeKey("ghost");

        var removed = await cache.RemoveAsync(key);
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveAsync_PendingEntry_RemovesAndReturnsTrue() {
        using var cache = new JsonFileCache(TempCachePath());
        var key = MakeKey();
        var entry = MakeEntry();

        await cache.SetAsync(key, entry);
        var removed = await cache.RemoveAsync(key);

        Assert.True(removed);
        var (found, _) = await cache.TryGetAsync(key);
        Assert.False(found);
    }

    [Fact]
    public async Task ClearAsync_DeletesFileAndClearsPending() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
            await cache.FlushAsync();
            Assert.True(File.Exists(path));

            await cache.ClearAsync();
            Assert.False(File.Exists(path));

            var (found, _) = await cache.TryGetAsync(key);
            Assert.False(found);
        }
    }

    [Fact]
    public async Task ClearAsync_NoFile_DoesNotThrow() {
        using var cache = new JsonFileCache(TempCachePath());

        await cache.ClearAsync();

        Assert.False(File.Exists(TempCachePath()));
    }

    [Fact]
    public async Task Flush_SyncFlush_PersistsToDisk() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
            cache.Flush();
        }

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task MultipleEntries_AllPersisted() {
        var path = TempCachePath();
        var key1 = MakeKey("A");
        var key2 = MakeKey("B");
        var entry1 = MakeEntry("Node1", 10);
        var entry2 = MakeEntry("Node2", 20);

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key1, entry1);
            await cache.SetAsync(key2, entry2);
            await cache.FlushAsync();
        }

        using (var cache2 = new JsonFileCache(path)) {
            var (found1, result1) = await cache2.TryGetAsync(key1);
            var (found2, result2) = await cache2.TryGetAsync(key2);

            Assert.True(found1);
            Assert.True(found2);
            Assert.Equal("Node1", result1!.NodeTypeName);
            Assert.Equal("Node2", result2!.NodeTypeName);
        }
    }

    [Fact]
    public async Task Dispose_WithoutFlush_PendingDataLost() {
        var path = TempCachePath();
        var key = MakeKey();
        var entry = MakeEntry();

        using (var cache = new JsonFileCache(path)) {
            await cache.SetAsync(key, entry);
        }

        using (var cache2 = new JsonFileCache(path)) {
            var (found, _) = await cache2.TryGetAsync(key);
            Assert.False(found);
        }
    }

    [Fact]
    public async Task FlushAsync_MergesWithExistingStore() {
        var path = TempCachePath();
        var key1 = MakeKey("A");
        var key2 = MakeKey("B");
        var entry1 = MakeEntry("Node1", 10);
        var entry2 = MakeEntry("Node2", 20);

        using (var cache1 = new JsonFileCache(path)) {
            await cache1.SetAsync(key1, entry1);
            await cache1.FlushAsync();
        }

        using (var cache2 = new JsonFileCache(path)) {
            await cache2.SetAsync(key2, entry2);
            await cache2.FlushAsync();
        }

        using (var cache3 = new JsonFileCache(path)) {
            var (found1, _) = await cache3.TryGetAsync(key1);
            var (found2, _) = await cache3.TryGetAsync(key2);

            Assert.True(found1);
            Assert.True(found2);
        }
    }
}

public class CachePortValueTests {
    [Fact]
    public void Constructor_SetsValueAndTypeName() {
        var cpv = new CachePortValue(42, typeof(int).AssemblyQualifiedName!);

        Assert.Equal(42, cpv.Value);
        Assert.Equal(typeof(int).AssemblyQualifiedName, cpv.TypeName);
    }

    [Fact]
    public void Constructor_NullValue_SetsValueToNull() {
        var cpv = new CachePortValue(null, "null");

        Assert.Null(cpv.Value);
        Assert.Equal("null", cpv.TypeName);
    }

    [Fact]
    public void Equality_SameValueAndType_AreEqual() {
        var a = new CachePortValue(42, "System.Int32");
        var b = new CachePortValue(42, "System.Int32");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual() {
        var a = new CachePortValue(42, "System.Int32");
        var b = new CachePortValue(99, "System.Int32");

        Assert.NotEqual(a, b);
    }
}

public class CacheEntryTests {
    [Fact]
    public void Constructor_SetsCachedAtToUtcNow() {
        var before = DateTimeOffset.UtcNow;

        var entry = new CacheEntry {
            Inputs = new Dictionary<string, CachePortValue>(),
            Outputs = new Dictionary<string, CachePortValue>(),
            NodeTypeName = "Test"
        };

        var after = DateTimeOffset.UtcNow;
        Assert.True(entry.CachedAt >= before);
        Assert.True(entry.CachedAt <= after);
    }

    [Fact]
    public void Properties_SetCorrectly() {
        var inputs = new Dictionary<string, CachePortValue> { ["a"] = new(1, "int") };
        var outputs = new Dictionary<string, CachePortValue> { ["b"] = new(2, "int") };

        var entry = new CacheEntry {
            Inputs = inputs,
            Outputs = outputs,
            NodeTypeName = "MyNode"
        };

        Assert.Equal("MyNode", entry.NodeTypeName);
        Assert.Single(entry.Inputs);
        Assert.Single(entry.Outputs);
        Assert.Equal(1, entry.Inputs["a"].Value);
        Assert.Equal(2, entry.Outputs["b"].Value);
    }
}
