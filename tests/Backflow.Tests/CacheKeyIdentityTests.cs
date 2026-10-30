using System.Globalization;
using Shiron.Backflow.Caching;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;
using Shiron.Backflow.Types;
using Xunit;

namespace Shiron.Backflow.Tests;

public sealed class CacheKeyIdentityTests {
    [Fact]
    public void Constructor_DelimiterComponents_CannotAliasDifferentIdentities() {
        Assert.NotEqual(new CacheKey("a|b", "c", "d").CombinedHash, new CacheKey("a", "b|c", "d").CombinedHash);
        Assert.NotEqual(new CacheKey("a", "b", "c").CombinedHash, new CacheKey("a", "v2", "c").CombinedHash);
    }

    [Fact]
    public void CreateKey_EquivalentDictionaries_AreOrderAndCultureIndependent() {
        var (node, instance, context) = Setup();
        var factory = new CacheKeyFactory();
        context.Write<object>(instance, node.Value, new Dictionary<string, int> { ["z"] = 1, ["ä"] = 2, ["a"] = 3 });
        var expected = factory.CreateKey(instance, context).CombinedHash;
        context.Write<object>(instance, node.Value, new Dictionary<string, int> { ["a"] = 3, ["ä"] = 2, ["z"] = 1 });
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, factory.CreateKey(instance, context).CombinedHash);
        } finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CreateKey_SameJsonDifferentRuntimeTypes_AreDifferent() {
        var (node, instance, context) = Setup();
        var factory = new CacheKeyFactory();
        context.Write<object>(instance, node.Value, 1);
        var integer = factory.CreateKey(instance, context).CombinedHash;
        context.Write<object>(instance, node.Value, 1.0);
        Assert.NotEqual(integer, factory.CreateKey(instance, context).CombinedHash);
    }

    [Fact]
    public void CreateKey_NodeSemanticSettings_InfluenceIdentityWithoutExecutionCounters() {
        var (node, instance, context) = Setup();
        var factory = new CacheKeyFactory();
        var expected = factory.CreateKey(instance, context).CombinedHash;
        node.Counter++;
        Assert.Equal(expected, factory.CreateKey(instance, context).CombinedHash);
        node.Settings = new Settings(2);
        Assert.NotEqual(expected, factory.CreateKey(instance, context).CombinedHash);
    }

    [Fact]
    public void CreateKey_OptionsVersionAndContextIdentitySelector_InfluenceIdentity() {
        var (_, instance, context) = Setup();
        var baseline = new CacheKeyFactory().CreateKey(instance, context).CombinedHash;
        var revised = new CacheKeyFactory(null, new CacheKeyFactoryOptions { Version = "2" });
        Assert.NotEqual(baseline, revised.CreateKey(instance, context).CombinedHash);
        var revision = 1;
        var contextual = new CacheKeyFactory(null, new CacheKeyFactoryOptions { IdentitySelector = (_, _) => new Settings(revision) });
        var first = contextual.CreateKey(instance, context).CombinedHash;
        revision++;
        Assert.NotEqual(first, contextual.CreateKey(instance, context).CombinedHash);
    }

    [Fact]
    public void CreateKey_BinaryStreamInputs_HashesContentNotOpaqueStreamObjects() {
        var (node, instance, context) = Setup();
        var factory = new CacheKeyFactory();
        using var first = new StreamData(() => new MemoryStream([1, 2, 3]));
        using var equal = new StreamData(() => new MemoryStream([1, 2, 3]));
        using var different = new StreamData(() => new MemoryStream([1, 2, 4]));
        context.Write<object>(instance, node.Value, first);
        var expected = factory.CreateKey(instance, context).CombinedHash;
        context.Write<object>(instance, node.Value, equal);
        Assert.Equal(expected, factory.CreateKey(instance, context).CombinedHash);
        context.Write<object>(instance, node.Value, different);
        Assert.NotEqual(expected, factory.CreateKey(instance, context).CombinedHash);
    }

    private static (IdentityNode Node, PipelineBuilder.NodeInstance Instance, ArrayPipelineContext Context) Setup() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new IdentityNode();
        var instance = builder.AddNode(node);
        return (node, instance, ArrayPipelineContext.ForPipeline(builder.Build()));
    }

    private sealed class IdentityNode : AbstractNode, ICacheIdentityProvider {
        public IInputPort<object> Value { get; }
        public Settings Settings { get; set; } = new(1);
        public int Counter { get; set; }
        public object CacheIdentity => Settings;
        public IdentityNode() => Value = Input(new InputPort<object>("value", null, new PassValidator()));
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => ValueTask.FromResult(true);
    }
    private sealed record Settings(int Revision);
    private sealed class PassValidator : IPortValidator<object> {
        public string? Validate(object? value) => null;
    }
}
