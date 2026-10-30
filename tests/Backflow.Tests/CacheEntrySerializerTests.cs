using Shiron.Backflow.Caching;
using Xunit;

namespace Shiron.Backflow.Tests;

public sealed class CacheEntrySerializerTests {
    private static CacheKey Key(int i = 0) => new("node", "1", i.ToString());
    private static CacheEntry Entry(int i) => new() {
        NodeTypeName = "node", Inputs = new Dictionary<string, CachePortValue>(),
        Outputs = new Dictionary<string, CachePortValue> { ["result"] = new(new Result(i), typeof(Result).AssemblyQualifiedName!) }
    };

    [Fact]
    public void Serialize_SharedAcrossConcurrentCalls_RestoresIndependentTypedSnapshots() {
        var serializer = new CacheEntrySerializer();
        var snapshots = new SerializedCacheEntry[64];
        Parallel.For(0, snapshots.Length, i => snapshots[i] = serializer.Serialize(Key(i), Entry(i)));
        foreach (var i in Enumerable.Range(0, snapshots.Length)) {
            Assert.True(serializer.TryDeserialize(Key(i), snapshots[i], out var entry));
            Assert.Equal(new Result(i), entry!.Outputs["result"].Value);
        }
    }

    [Fact]
    public void TryDeserialize_AllowedTypesCollectionChanges_DoNotMutateSerializerPolicy() {
        var types = new List<Type> { typeof(Result) };
        var serializer = new CacheEntrySerializer(new() { AllowedTypes = types });
        types.Clear();
        var snapshot = new CacheEntrySerializer().Serialize(Key(), Entry(9));
        Assert.True(serializer.TryDeserialize(Key(), snapshot, out var entry));
        Assert.Equal(new Result(9), entry!.Outputs["result"].Value);
    }

    [Fact]
    public void Serialize_CanceledBeforeEmission_DoesNotTrustRuntimeType() {
        var serializer = new CacheEntrySerializer();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => serializer.Serialize(Key(), Entry(9), cancel.Token));
        var trustedElsewhere = new CacheEntrySerializer().Serialize(Key(), Entry(9));
        Assert.False(serializer.TryDeserialize(Key(), trustedElsewhere, out _));
    }

    private sealed record Result(int Value);
}
