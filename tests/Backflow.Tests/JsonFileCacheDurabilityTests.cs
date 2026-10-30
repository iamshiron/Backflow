using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;
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
    public async Task TryGetAsync_ReopenedWithSmallerPendingBudget_PreservesLargeCommittedEntryThroughFlushAndReopen() {
        var value = new string('x', 8192);
        var largerBudget = new JsonFileCacheOptions { MaximumEntryBytes = 64 * 1024, MaximumPendingBytes = 64 * 1024 };
        var smallerBudget = new JsonFileCacheOptions { MaximumEntryBytes = 64 * 1024, MaximumPendingBytes = 4096 };
        using (var writer = new JsonFileCache(CachePath, options: largerBudget)) {
            await writer.SetAsync(Key(), EntryWithValue(value));
            await writer.FlushAsync();
        }
        var originalFile = await File.ReadAllBytesAsync(CachePath);
        var originalEnvelope = JsonNode.Parse(originalFile)!["Entries"]![Key().CombinedHash]!.ToJsonString();
        using (var reader = new JsonFileCache(CachePath, options: smallerBudget)) {
            var hit = await reader.TryGetAsync(Key());
            Assert.True(hit.Found);
            Assert.Equal(value, hit.Entry!.Outputs["value"].Value);
            Assert.Equal(originalFile, await File.ReadAllBytesAsync(CachePath));

            await Assert.ThrowsAsync<InvalidDataException>(() => reader.SetAsync(Key(), EntryWithValue(new string('y', 8192))).AsTask());
            await reader.SetAsync(Key("small"), Entry(9));
            await reader.FlushAsync();
            Assert.Equal(value, (await reader.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        }
        using var reopened = new JsonFileCache(CachePath, options: smallerBudget);
        Assert.Equal(value, (await reopened.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        Assert.Equal(9, (await reopened.TryGetAsync(Key("small"))).Entry!.Outputs["value"].Value);
        Assert.Equal(originalEnvelope, JsonNode.Parse(await File.ReadAllBytesAsync(CachePath))!["Entries"]![Key().CombinedHash]!.ToJsonString());
    }

    [Fact]
    public async Task TryGetAsync_LiveInstancesWithDifferentPendingBudgets_PreserveEachOthersCommittedValues() {
        using var writer = new JsonFileCache(CachePath, options: new JsonFileCacheOptions {
            MaximumEntryBytes = 64 * 1024, MaximumPendingBytes = 64 * 1024
        });
        var smallerBudget = new JsonFileCacheOptions { MaximumEntryBytes = 64 * 1024, MaximumPendingBytes = 4096 };
        using var reader = new JsonFileCache(CachePath, options: smallerBudget);
        var first = new string('x', 8192);
        await writer.SetAsync(Key(), EntryWithValue(first));
        await writer.FlushAsync();
        Assert.Equal(first, (await reader.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        await reader.SetAsync(Key("small"), Entry(7));
        await reader.FlushAsync();
        Assert.Equal(7, (await writer.TryGetAsync(Key("small"))).Entry!.Outputs["value"].Value);

        var replacement = new string('y', 8192);
        await writer.SetAsync(Key(), EntryWithValue(replacement));
        await writer.FlushAsync();
        Assert.Equal(replacement, (await reader.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        await reader.FlushAsync();
        using var reopened = new JsonFileCache(CachePath, options: smallerBudget);
        Assert.Equal(replacement, (await reopened.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
    }

    [Fact]
    public async Task SetAsync_LargeLazyEnumerable_StopsDuringEmissionAndPreservesPendingAndPersistedEntries() {
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { MaximumEntryBytes = 1024 });
        await cache.SetAsync(Key(), Entry(1));
        await cache.FlushAsync();
        var persisted = await File.ReadAllBytesAsync(CachePath);
        await cache.SetAsync(Key(), Entry(2));
        var values = new CountingValues();

        await Assert.ThrowsAsync<InvalidDataException>(() => cache.SetAsync(Key(), EntryWithValue(values)).AsTask());

        Assert.InRange(values.Visited, 1, 4096);
        Assert.Equal(2, (await cache.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        Assert.Equal(persisted, await File.ReadAllBytesAsync(CachePath));
        await cache.FlushAsync();
        using var reopened = new JsonFileCache(CachePath);
        Assert.Equal(2, (await reopened.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetAsync_LargeIncrementalConverter_EnforcesEntryAndPendingLimitsWithoutTrustingRejectedTypes(bool constrainPending) {
        var adapters = new CacheTypeAdapterRegistry();
        adapters.Register(new CountingOutputConverter());
        using var cache = new JsonFileCache(CachePath, adapters, new JsonFileCacheOptions {
            MaximumEntryBytes = constrainPending ? 1024 * 1024 : 1024,
            MaximumPendingBytes = constrainPending ? 1024 : 1024 * 1024
        });
        await cache.SetAsync(Key(), Entry());
        var output = new CountingOutput();

        await Assert.ThrowsAsync<InvalidDataException>(() => cache.SetAsync(Key(), EntryWithValue(output)).AsTask());

        Assert.InRange(output.Emitted, 1, 1024);
        Assert.Equal(1, (await cache.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
        var rejectedTypeNull = new CacheEntry {
            NodeTypeName = "node", Inputs = new Dictionary<string, CachePortValue>(),
            Outputs = new Dictionary<string, CachePortValue> { ["value"] = new(null, typeof(CountingOutput).AssemblyQualifiedName!) }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => cache.SetAsync(Key("untrusted"), rejectedTypeNull).AsTask());
        await cache.FlushAsync();
        using var reopened = new JsonFileCache(CachePath);
        Assert.Equal(1, (await reopened.TryGetAsync(Key())).Entry!.Outputs["value"].Value);
    }

    [Fact]
    public async Task SetAsync_LargeEscapedString_RejectsBeforeUnboundedEscapingAllocation() {
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { MaximumEntryBytes = 1024 });
        await cache.SetAsync(Key(), Entry());
        var entry = EntryWithValue(new string('\n', 1024 * 1024));
        var before = GC.GetAllocatedBytesForCurrentThread();
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.SetAsync(Key(), entry).AsTask());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 256 * 1024);
        Assert.True((await cache.TryGetAsync(Key())).Found);
    }

    [Fact]
    public async Task SetAsync_LargeEscapedJsonElement_RejectsBeforeUnboundedTokenUnescaping() {
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { MaximumEntryBytes = 1024 });
        await cache.SetAsync(Key(), Entry());
        using var document = JsonDocument.Parse("\"" + string.Concat(Enumerable.Repeat("\\n", 1024 * 1024)) + "\"");
        var entry = EntryWithValue(document.RootElement);
        var before = GC.GetAllocatedBytesForCurrentThread();
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.SetAsync(Key(), entry).AsTask());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 256 * 1024);
        Assert.True((await cache.TryGetAsync(Key())).Found);
    }

    [Fact]
    public async Task FlushAsync_StoreByteLimit_EvictsBeforeMaterializingOversizedStore() {
        using var cache = new JsonFileCache(CachePath, options: new JsonFileCacheOptions { MaximumStoreBytes = 2048 });
        foreach (var i in Enumerable.Range(0, 10)) await cache.SetAsync(Key(i.ToString()), Entry(i));
        await cache.FlushAsync();
        Assert.InRange(new FileInfo(CachePath).Length, 1, 2048);
        using var reopened = new JsonFileCache(CachePath);
        Assert.True((await reopened.TryGetAsync(Key("9"))).Found);
        Assert.False((await reopened.TryGetAsync(Key("0"))).Found);
    }

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
    private static CacheEntry EntryWithValue(object value) => new() {
        NodeTypeName = "node", Inputs = new Dictionary<string, CachePortValue>(),
        Outputs = new Dictionary<string, CachePortValue> { ["value"] = new(value, value.GetType().AssemblyQualifiedName!) }
    };
    private sealed class CountingValues : IEnumerable<int> {
        public int Visited { get; private set; }
        public IEnumerator<int> GetEnumerator() {
            for (var i = 0; i < 1_000_000; i++) {
                Visited++;
                yield return i;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class CountingOutput {
        public int Emitted { get; set; }
    }
    private sealed class CountingOutputConverter : JsonConverter<CountingOutput> {
        public override CountingOutput Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("Only rejected writes are tested.");
        public override void Write(Utf8JsonWriter writer, CountingOutput value, JsonSerializerOptions options) {
            writer.WriteStartArray();
            for (var i = 0; i < 1_000_000; i++) {
                value.Emitted++;
                writer.WriteNumberValue(i);
                writer.Flush();
            }
            writer.WriteEndArray();
        }
    }
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
