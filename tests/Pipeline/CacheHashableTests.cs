using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Types;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class CacheHashableTests {
    [Fact]
    public void MemoryBlob_GetCacheHash_IsStable() {
        var blob = new MemoryBlob { Data = [1, 2, 3, 4, 5] };
        var hash1 = blob.GetCacheHash();
        var hash2 = blob.GetCacheHash();
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void MemoryBlob_GetCacheHash_DifferentData_ProducesDifferentHash() {
        var blob1 = new MemoryBlob { Data = [1, 2, 3] };
        var blob2 = new MemoryBlob { Data = [4, 5, 6] };
        Assert.NotEqual(blob1.GetCacheHash(), blob2.GetCacheHash());
    }

    [Fact]
    public void MemoryBlob_GetCacheHash_SameData_ProducesSameHash() {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var blob1 = new MemoryBlob { Data = data };
        var blob2 = new MemoryBlob { Data = [.. data] };
        Assert.Equal(blob1.GetCacheHash(), blob2.GetCacheHash());
    }

    [Fact]
    public void MemoryBlob_SetData_InvalidatesHash() {
        var blob = new MemoryBlob { Data = [1, 2, 3] };
        var hash1 = blob.GetCacheHash();
        blob.Data = [4, 5, 6];
        var hash2 = blob.GetCacheHash();
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void MemoryImageBlob_InheritsCacheHash() {
        var blob = new MemoryImageBlob { Data = [1, 2, 3], Width = 100, Height = 200, Channels = 3 };
        var hash = blob.GetCacheHash();
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public void MemoryImageBlob_MetadataDoesNotAffectHash() {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var blob1 = new MemoryImageBlob { Data = [.. data], Width = 100, Height = 200, Channels = 3 };
        var blob2 = new MemoryImageBlob { Data = [.. data], Width = 999, Height = 888, Channels = 1 };
        Assert.Equal(blob1.GetCacheHash(), blob2.GetCacheHash());
    }

    [Fact]
    public void MemoryAudioBlob_InheritsCacheHash() {
        var blob = new MemoryAudioBlob { Data = [1, 2, 3], SampleRate = 44100, Channels = 2 };
        var hash = blob.GetCacheHash();
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public void CacheKey_WithCacheHashable_UsesHashNotJson() {
        var node = new TestNode();
        var blob = new MemoryBlob { Data = [1, 2, 3, 4, 5] };
        var key = CacheKey.Create(node, [("data", typeof(MemoryBlob), (object?) blob)]);
        Assert.NotNull(key.InputHash);
        Assert.NotEmpty(key.InputHash);
    }

    [Fact]
    public void CacheKey_MixedInputs_CacheHashableAndPrimitive() {
        var node = new TestNode();
        var blob = new MemoryBlob { Data = [1, 2, 3] };
        var key1 = CacheKey.Create(node, [
            ("data", typeof(MemoryBlob), (object?)blob),
            ("count", typeof(int), (object?)42)
        ]);
        var key2 = CacheKey.Create(node, [
            ("data", typeof(MemoryBlob), (object?)blob),
            ("count", typeof(int), (object?)42)
        ]);
        Assert.Equal(key1, key2);

        var key3 = CacheKey.Create(node, [
            ("data", typeof(MemoryBlob), (object?)blob),
            ("count", typeof(int), (object?)99)
        ]);
        Assert.NotEqual(key1, key3);
    }

    [Fact]
    public void CacheKey_CacheHashable_SameData_SameKey() {
        var node = new TestNode();
        var data = new byte[] { 10, 20, 30 };
        var blob1 = new MemoryBlob { Data = [.. data] };
        var blob2 = new MemoryBlob { Data = [.. data] };

        var key1 = CacheKey.Create(node, [("d", typeof(MemoryBlob), (object?) blob1)]);
        var key2 = CacheKey.Create(node, [("d", typeof(MemoryBlob), (object?) blob2)]);
        Assert.Equal(key1, key2);
    }

    private class TestNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }
}
