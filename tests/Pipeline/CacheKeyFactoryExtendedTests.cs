using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class CacheKeyFactoryExtendedTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class GenericNode<T> : AbstractGenericNode {
        public readonly IInputPort<T> Value;
        public readonly IOutputPort<T> Result;

        public GenericNode() {
            Value = Input(new InputPort<T>("value", default, new PassValidator<T>()));
            Result = Output(new OutputPort<T>("result"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class NullableInputNode : AbstractNode {
        public readonly IInputPort<string?> Text;
        public readonly IOutputPort<int> Length;

        public NullableInputNode() {
            Text = Input(new InputPort<string?>("text", null, new PassValidator<string?>()));
            Length = Output(new OutputPort<int>("length"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void CreateKey_NullInputValue_ProducesDeterministicKey() {
        var builder = new PipelineBuilder(_registry);
        var node = new NullableInputNode();
        var inst = builder.AddNode(node);

        var ctx1 = new PipelineContext();
        var ctx2 = new PipelineContext();

        var factory = new CacheKeyFactory();
        var key1 = factory.CreateKey(inst, ctx1);
        var key2 = factory.CreateKey(inst, ctx2);

        Assert.Equal(key1.InputHash, key2.InputHash);
        Assert.Equal(key1.CombinedHash, key2.CombinedHash);
    }

    [Fact]
    public void CreateKey_NullVsNonNullInput_ProducesDifferentKeys() {
        var builder = new PipelineBuilder(_registry);
        var node = new NullableInputNode();
        var inst = builder.AddNode(node);

        var ctxNull = new PipelineContext();

        var ctxNonNull = new PipelineContext();
        ctxNonNull.Write<string?>(inst, node.Text, "hello");

        var factory = new CacheKeyFactory();
        var keyNull = factory.CreateKey(inst, ctxNull);
        var keyNonNull = factory.CreateKey(inst, ctxNonNull);

        Assert.NotEqual(keyNull.InputHash, keyNonNull.InputHash);
    }

    [Fact]
    public void CreateKey_GenericNodeType_ContainsGenericArgsInTypeName() {
        var registry = new NodeRegistry();
        var bp = registry.RegisterGeneric(typeof(GenericNode<>));
        var builder = new PipelineBuilder(registry);
        var inst = builder.AddNode(bp, [typeof(int)]);

        var ctx = new PipelineContext();

        var factory = new CacheKeyFactory();
        var key = factory.CreateKey(inst, ctx);

        Assert.Contains("Int32", key.NodeType);
        Assert.Contains("GenericNode", key.NodeType);
    }

    [Fact]
    public void CreateKey_GenericNodeWithDifferentTypeArgs_ProducesDifferentKeys() {
        var registry = new NodeRegistry();
        var bp = registry.RegisterGeneric(typeof(GenericNode<>));
        var builder = new PipelineBuilder(registry);

        var instInt = builder.AddNode(bp, [typeof(int)]);
        var instDouble = builder.AddNode(bp, [typeof(double)]);

        var factory = new CacheKeyFactory();
        var ctx = new PipelineContext();

        var keyInt = factory.CreateKey(instInt, ctx);
        var keyDouble = factory.CreateKey(instDouble, ctx);

        Assert.NotEqual(keyInt.NodeType, keyDouble.NodeType);
        Assert.NotEqual(keyInt.CombinedHash, keyDouble.CombinedHash);
    }

    [Fact]
    public void CreateKey_SameNullInputTwice_ProducesSameKey() {
        var builder = new PipelineBuilder(_registry);
        var node = new NullableInputNode();
        var inst = builder.AddNode(node);

        var ctx1 = new PipelineContext();
        ctx1.Write<string?>(inst, node.Text, null);

        var ctx2 = new PipelineContext();
        ctx2.Write<string?>(inst, node.Text, null);

        var factory = new CacheKeyFactory();
        var key1 = factory.CreateKey(inst, ctx1);
        var key2 = factory.CreateKey(inst, ctx2);

        Assert.Equal(key1.CombinedHash, key2.CombinedHash);
    }

    [Fact]
    public void CreateKey_AssemblyVersionIsPopulated() {
        var builder = new PipelineBuilder(_registry);
        var node = new NullableInputNode();
        var inst = builder.AddNode(node);

        var ctx = new PipelineContext();

        var factory = new CacheKeyFactory();
        var key = factory.CreateKey(inst, ctx);

        Assert.NotEmpty(key.AssemblyVersion);
        Assert.NotEqual("0.0.0.0", key.AssemblyVersion);
    }

    [Fact]
    public void CreateKey_WithCacheTypeAdapter_ProducesConsistentKeys() {
        var builder = new PipelineBuilder(_registry);
        var node = new NullableInputNode();
        var inst = builder.AddNode(node);

        var ctx = new PipelineContext();
        ctx.Write<string?>(inst, node.Text, "test");

        var factory = new CacheKeyFactory(new CacheTypeAdapterRegistry());
        var key1 = factory.CreateKey(inst, ctx);
        var key2 = factory.CreateKey(inst, ctx);

        Assert.Equal(key1.CombinedHash, key2.CombinedHash);
    }
}

public class CacheKeyTests {
    [Fact]
    public void Constructor_ComputesCombinedHash_FromComponents() {
        var key = new CacheKey("MyNode", "1.0.0.0", "abc123");

        Assert.Equal("MyNode", key.NodeType);
        Assert.Equal("1.0.0.0", key.AssemblyVersion);
        Assert.Equal("abc123", key.InputHash);
        Assert.NotEmpty(key.CombinedHash);
    }

    [Fact]
    public void Equals_SameComponents_ReturnsTrue() {
        var key1 = new CacheKey("Node", "1.0", "hash");
        var key2 = new CacheKey("Node", "1.0", "hash");

        Assert.True(key1.Equals(key2));
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentNodeType_ReturnsFalse() {
        var key1 = new CacheKey("NodeA", "1.0", "hash");
        var key2 = new CacheKey("NodeB", "1.0", "hash");

        Assert.False(key1.Equals(key2));
    }

    [Fact]
    public void Equals_DifferentInputHash_ReturnsFalse() {
        var key1 = new CacheKey("Node", "1.0", "hash1");
        var key2 = new CacheKey("Node", "1.0", "hash2");

        Assert.False(key1.Equals(key2));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse() {
        var key = new CacheKey("Node", "1.0", "hash");

        Assert.False(key.Equals((ICacheKey?) null));
    }

    [Fact]
    public void ToString_ReturnsCombinedHash() {
        var key = new CacheKey("Node", "1.0", "hash");

        Assert.Equal(key.CombinedHash, key.ToString());
    }
}
