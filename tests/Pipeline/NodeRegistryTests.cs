using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class NodeRegistryTests {
    private class StubNodeA : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class StubNodeB : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    [Fact]
    public void Register_Generic_CreatesInstanceAndRegisters() {
        var registry = new NodeRegistry();
        var node = registry.Register<StubNodeA>();
        Assert.NotNull(node);
        Assert.IsType<StubNodeA>(node);
        Assert.Same(node, registry.Get<StubNodeA>());
    }

    [Fact]
    public void Register_DuplicateType_ThrowsArgumentException() {
        var registry = new NodeRegistry();
        registry.Register(new StubNodeA());
        Assert.Throws<ArgumentException>(() => registry.Register(new StubNodeA()));
    }

    [Fact]
    public void GetByFullName_MatchesFullNameNotSimpleName() {
        var registry = new NodeRegistry();
        var node = new StubNodeA();
        registry.Register(node);
        Assert.Same(node, registry.GetByFullName(typeof(StubNodeA).FullName!));
        Assert.Null(registry.GetByFullName(typeof(StubNodeA).Name));
    }
}
