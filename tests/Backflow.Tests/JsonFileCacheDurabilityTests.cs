using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Shiron.Backflow.Caching;
using Xunit;

namespace Shiron.Backflow.Tests;

// Integration tests: real rename, file sharing, fsync, and symlink boundaries cannot be validated by a memory double.
public sealed class JsonFileCacheDurabilityTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "backflow-cache-" + Guid.NewGuid().ToString("N"));
    private string CachePath => Path.Combine(_directory, "cache.json");
    private static CacheKey Key(string id = "a") => new("node", "1", id);
    private static CacheEntry Entry(int value = 1, DateTimeOffset? at = null) => new() {
        NodeTypeName = "node", CachedAt = at ?? DateTimeOffset.UtcNow,
        Inputs = new Dictionary<string, CachePortValue>(),
        Outputs = new Dictionary<string, CachePortValue> { ["value"] = new(value, typeof(int).AssemblyQualifiedName!) }
    };

    [Fact]
    public async Task TryGetAsync_CustomCombinedHashCollision_VerifiesCompleteKeyBeforeHit() {
        using var cache = new JsonFileCache(CachePath);
        await cache.SetAsync(Key(), Entry());
        await cache.FlushAsync();
        var collision = new CollisionKey("different-node", Key().AssemblyVersion, Key().InputHash, Key().CombinedHash);
        Assert.False((await cache.TryGetAsync(collision)).Found);
    }

    [Fact]
    public async Task FlushAsync_PublishFailure_PreservesOldFileAndPendingRetry() {
        using (var initial = new JsonFileCache(CachePath)) {
            await initial.SetAsync(Key(), Entry());
            await initial.FlushAsync();
        }
        var old = await File.ReadAllBytesAsync(CachePath);
        var fail = true;
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions {
            BeforePublish = (temp, _) => {
                Assert.NotEmpty(File.ReadAllBytes(temp));
                if (fail) throw new IOException("Injected failure after fsync, before rename.");
            }
        });
        await cache.SetAsync(Key(), Entry(2));
        await Assert.ThrowsAsync<IOException>(cache.FlushAsync);
        Assert.Equal(old, await File.ReadAllBytesAsync(CachePath));
        Assert.Equal(2, (await cache.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp-*"));
        fail = false;
        await cache.FlushAsync();
        using var reopened = new JsonFileCache(CachePath);
        Assert.Equal(2, (await reopened.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
    }

    [Fact]
    public async Task FlushAsync_ConcurrentInstances_MergesWithoutLostUpdates() {
        var caches = Enumerable.Range(0, 20).Select(_ => new JsonFileCache(CachePath)).ToArray();
        try {
            await Task.WhenAll(caches.Select(async (cache, i) => {
                await cache.SetAsync(Key(i.ToString()), Entry(i));
                await cache.FlushAsync();
            }));
            using var reader = new JsonFileCache(CachePath);
            foreach (var i in Enumerable.Range(0, caches.Length))
                Assert.Equal(i, (await reader.TryGetAsync(Key(i.ToString()))).Entry!.Outputs["value"].Value);
        } finally { foreach (var cache in caches) cache.Dispose(); }
    }

    [Fact]
    public async Task TryGetAsync_ChangedPayload_InvalidatesInsteadOfReturningHit() {
        using var cache = new JsonFileCache(CachePath);
        await cache.SetAsync(Key(), Entry());
        await cache.FlushAsync();
        var root = JsonNode.Parse(await File.ReadAllTextAsync(CachePath))!;
        root["Entries"]![Key().CombinedHash]!["Payload"] = "{}";
        await File.WriteAllTextAsync(CachePath, root.ToJsonString());
        Assert.False((await cache.TryGetAsync(Key())).Found);
        Assert.False(JsonNode.Parse(await File.ReadAllTextAsync(CachePath))!["Entries"]!.AsObject().ContainsKey(Key().CombinedHash));
    }

    [Fact]
    public async Task TryGetAsync_ValidChecksumButUntrustedType_InvalidatesWithoutActivatingType() {
        using var cache = new JsonFileCache(CachePath);
        await cache.SetAsync(Key(), Entry());
        await cache.FlushAsync();
        var root = JsonNode.Parse(await File.ReadAllTextAsync(CachePath))!;
        var envelope = root["Entries"]![Key().CombinedHash]!;
        var payload = JsonNode.Parse(envelope["Payload"]!.GetValue<string>())!;
        payload["Outputs"]!["value"]!["TypeName"] = typeof(UntrustedValue).AssemblyQualifiedName;
        payload["Outputs"]!["value"]!["Value"] = new JsonObject();
        var json = payload.ToJsonString();
        envelope["Payload"] = json;
        envelope["Checksum"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        await File.WriteAllTextAsync(CachePath, root.ToJsonString());
        Assert.False((await cache.TryGetAsync(Key())).Found);
    }

    [Fact]
    public async Task TryGetAsync_TruncatedStore_QuarantinesAndReturnsMiss() {
        using var cache = new JsonFileCache(CachePath);
        await File.WriteAllTextAsync(CachePath, "{\"Entries\":");
        Assert.False((await cache.TryGetAsync(Key())).Found);
        Assert.True(File.Exists(CachePath + ".corrupt"));
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task TryGetAsync_AbandonedTemp_IgnoresAndCleansUnpublishedWrite() {
        using var cache = new JsonFileCache(CachePath);
        await File.WriteAllTextAsync(CachePath + ".tmp-abandoned", "{}");
        Assert.False((await cache.TryGetAsync(Key())).Found);
        Assert.False(File.Exists(CachePath + ".tmp-abandoned"));
    }

    [Fact]
    public async Task SetAsync_MutableValue_TakesSnapshotAndReturnsIndependentValues() {
        using var cache = new JsonFileCache(CachePath);
        var values = new[] { 1, 2 };
        await cache.SetAsync(Key(), new CacheEntry {
            NodeTypeName = "node", Inputs = new Dictionary<string, CachePortValue>(),
            Outputs = new Dictionary<string, CachePortValue> { ["value"] = new(values, typeof(int[]).AssemblyQualifiedName!) }
        });
        values[0] = 99;
        var result = Assert.IsType<int[]>((await cache.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        Assert.Equal(1, result[0]);
        result[0] = 77;
        Assert.Equal(1, Assert.IsType<int[]>((await cache.TryGetAsync(Key())).Entry!.Outputs["value"].Value)[0]);
    }

    [Fact]
    public async Task TryGetAsync_CustomPersistedType_RequiresExplicitAllowlistOnReopen() {
        using (var writer = new JsonFileCache(CachePath)) {
            await writer.SetAsync(Key(), new CacheEntry {
                NodeTypeName = "node", Inputs = new Dictionary<string, CachePortValue>(),
                Outputs = new Dictionary<string, CachePortValue> { ["value"] = new(new TrustedValue(7), typeof(TrustedValue).AssemblyQualifiedName!) }
            });
            await writer.FlushAsync();
        }
        using var reader = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { AllowedTypes = [typeof(TrustedValue)] });
        Assert.Equal(new TrustedValue(7), (await reader.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
    }

    [Fact]
    public async Task SetAsync_CapacityAndLifetimeBounds_EvictsOldEntriesAndExpiresPersistedEntries() {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions {
            MaximumEntries = 2, MaximumAge = TimeSpan.FromMinutes(1), TimeProvider = clock
        });
        for (var i = 0; i < 3; i++) {
            await cache.SetAsync(Key(i.ToString()), Entry(i, clock.GetUtcNow()));
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        Assert.False((await cache.TryGetAsync(Key("0"))).Found);
        await cache.FlushAsync();
        Assert.True((await cache.TryGetAsync(Key("2"))).Found);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False((await cache.TryGetAsync(Key("2"))).Found);
        Assert.Empty(JsonNode.Parse(await File.ReadAllTextAsync(CachePath))!["Entries"]!.AsObject());
    }

    [Fact]
    public async Task SetAsync_OversizedEntry_RejectsWithoutDiscardingExistingEntry() {
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { MaximumEntryBytes = 1000 });
        await cache.SetAsync(Key(), Entry());
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.SetAsync(Key(), new CacheEntry {
            NodeTypeName = new string('x', 2000), Inputs = new Dictionary<string, CachePortValue>(), Outputs = new Dictionary<string, CachePortValue>()
        }).AsTask());
        Assert.True((await cache.TryGetAsync(Key())).Found);
    }

    [Fact]
    public async Task RemoveAsync_PublishFailure_DoesNotDiscardPendingValue() {
        using (var initial = new JsonFileCache(CachePath)) {
            await initial.SetAsync(Key(), Entry());
            await initial.FlushAsync();
        }
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { BeforePublish = (_, _) => throw new IOException("failure") });
        await cache.SetAsync(Key(), Entry(2));
        await Assert.ThrowsAsync<IOException>(() => cache.RemoveAsync(Key()).AsTask());
        Assert.Equal(2, (await cache.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
    }

    [Fact]
    public void Constructor_SymbolicLink_RejectsOutsideBoundary() {
        Directory.CreateDirectory(_directory);
        var outside = Path.Combine(_directory, "outside.json");
        File.CreateSymbolicLink(CachePath, outside);
        Assert.Throws<IOException>(() => new JsonFileCache(CachePath));
        Assert.False(File.Exists(outside));
    }

    [Fact]
    public async Task FlushAsync_AnotherProcessOwnsLock_TimesOutWithoutLosingPendingWrite() {
        // flock is the Linux OS primitive used by .NET file sharing; non-Linux sharing is covered by instance tests.
        if (!OperatingSystem.IsLinux()) return;
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { LockTimeout = TimeSpan.FromMilliseconds(50) });
        var start = new ProcessStartInfo("flock") { RedirectStandardOutput = true, RedirectStandardInput = true };
        foreach (var argument in new[] { "-x", CachePath + ".lock", "sh", "-c", "echo locked; read release" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        try {
            Assert.Equal("locked", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            await cache.SetAsync(Key(), Entry());
            await Assert.ThrowsAsync<IOException>(cache.FlushAsync);
            Assert.True((await cache.TryGetAsync(Key())).Found);
        } finally {
            await process.StandardInput.WriteLineAsync("release");
            await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        await cache.FlushAsync();
        Assert.True(File.Exists(CachePath));
    }

    [Fact]
    public async Task TryGetAsync_Disposed_ThrowsPredictably() {
        var cache = new JsonFileCache(CachePath);
        cache.Dispose();
        cache.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.TryGetAsync(Key()).AsTask());
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed record TrustedValue(int Number);
    private sealed record CollisionKey(string NodeType, string AssemblyVersion, string InputHash, string CombinedHash) : ICacheKey;
    private sealed class UntrustedValue {
        public UntrustedValue() => throw new InvalidOperationException("Must never be activated from a file type name.");
    }
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
