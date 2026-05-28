using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class DefaultNodeActivatorTests {
    private sealed class TestNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    private sealed class DisposableNode : AbstractNode, IDisposable {
        public bool IsDisposed { get; private set; }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public void INodeActivator_ExtendsIDisposable() {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(INodeActivator)));
    }

    [Fact]
    public void CreateNode_Generic_ReturnsCorrectType() {
        using var activator = new DefaultNodeActivator();
        var node = activator.CreateNode<TestNode>();
        Assert.NotNull(node);
        Assert.IsType<TestNode>(node);
    }

    [Fact]
    public void CreateNode_Type_ReturnsCorrectType() {
        using var activator = new DefaultNodeActivator();
        var node = activator.CreateNode(typeof(TestNode));
        Assert.NotNull(node);
        Assert.IsType<TestNode>(node);
    }

    [Fact]
    public void CreateNode_Generic_CalledMultipleTimes_ReturnsNewInstances() {
        using var activator = new DefaultNodeActivator();
        var node1 = activator.CreateNode<TestNode>();
        var node2 = activator.CreateNode<TestNode>();
        Assert.NotSame(node1, node2);
    }

    [Fact]
    public void CreateNode_Type_WithNonAbstractNodeType_ThrowsArgumentException() {
        using var activator = new DefaultNodeActivator();
        Assert.Throws<ArgumentException>(() => activator.CreateNode(typeof(List<int>)));
    }

    [Fact]
    public void Dispose_DisposesTrackedDisposableNodes() {
        using var activator = new DefaultNodeActivator();
        var node = activator.CreateNode<DisposableNode>();
        Assert.False(node.IsDisposed);
        activator.Dispose();
        Assert.True(node.IsDisposed);
    }

    [Fact]
    public void Dispose_DisposesAllTrackedNodes() {
        using var activator = new DefaultNodeActivator();
        var node1 = activator.CreateNode<DisposableNode>();
        var node2 = activator.CreateNode<DisposableNode>();
        var node3 = activator.CreateNode<DisposableNode>();
        activator.Dispose();
        Assert.True(node1.IsDisposed);
        Assert.True(node2.IsDisposed);
        Assert.True(node3.IsDisposed);
    }

    [Fact]
    public void Dispose_NonDisposableNodesAreUnaffected() {
        using var activator = new DefaultNodeActivator();
        activator.CreateNode<TestNode>();
        activator.Dispose();
    }

    [Fact]
    public void Dispose_DoubleDisposeDoesNotThrow() {
        var activator = new DefaultNodeActivator();
        activator.CreateNode<TestNode>();
        activator.Dispose();
        activator.Dispose();
    }
}
