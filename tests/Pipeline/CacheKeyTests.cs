using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class CacheKeyTests {
    private class TestNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private static CacheKey CreateKey(AbstractNode node, params (string, Type, object?)[] inputs) {
        return CacheKey.Create(node, inputs);
    }

    [Fact]
    public void Create_SameInputs_ProducesSameKey() {
        var node = new TestNode();
        var inputs = new[] { ("port1", typeof(int), (object?) 42) };
        var key1 = CreateKey(node, inputs);
        var key2 = CreateKey(node, inputs);
        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Create_DifferentInputs_ProducesDifferentKey() {
        var node = new TestNode();
        var key1 = CreateKey(node, ("port1", typeof(int), (object?) 1));
        var key2 = CreateKey(node, ("port1", typeof(int), (object?) 2));
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Create_DifferentPortNames_ProducesDifferentKey() {
        var node = new TestNode();
        var key1 = CreateKey(node, ("portA", typeof(int), (object?) 1));
        var key2 = CreateKey(node, ("portB", typeof(int), (object?) 1));
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Create_NullValue_ProducesValidKey() {
        var node = new TestNode();
        var key = CreateKey(node, ("port1", typeof(string), (object?) null));
        Assert.NotNull(key);
        Assert.NotEmpty(key.InputHash);
    }

    [Fact]
    public void Create_EmptyInputs_ProducesValidKey() {
        var node = new TestNode();
        var key = CreateKey(node);
        Assert.NotNull(key);
        Assert.NotEmpty(key.InputHash);
    }

    [Fact]
    public void Create_SetsNodeType() {
        var node = new TestNode();
        var key = CreateKey(node);
        Assert.Equal(typeof(TestNode).FullName, key.NodeType);
    }

    [Fact]
    public void ToCompositeKey_ContainsAllParts() {
        var node = new TestNode();
        var key = CreateKey(node, ("p", typeof(int), (object?) 1));
        var composite = key.ToCompositeKey();
        Assert.Contains(key.NodeType, composite);
        Assert.Contains(key.AssemblyVersion, composite);
        Assert.Contains(key.InputHash, composite);
    }

    [Fact]
    public void ToCompositeKey_UsesColonSeparator() {
        var node = new TestNode();
        var key = CreateKey(node);
        var composite = key.ToCompositeKey();
        var parts = composite.Split(':');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse() {
        var node = new TestNode();
        var key = CreateKey(node);
        Assert.False(key.Equals(null));
    }

    [Fact]
    public void Equals_DifferentObject_ReturnsFalse() {
        var node = new TestNode();
        var key = CreateKey(node);
        Assert.False(key.Equals("not a key"));
    }

    [Fact]
    public void ToString_ReturnsCompositeKey() {
        var node = new TestNode();
        var key = CreateKey(node);
        Assert.Equal(key.ToCompositeKey(), key.ToString());
    }

    [Fact]
    public void GetHashCode_SameKey_ReturnsSameHash() {
        var node = new TestNode();
        var key1 = CreateKey(node, ("p", typeof(int), (object?) 1));
        var key2 = CreateKey(node, ("p", typeof(int), (object?) 1));
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }
}
