using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Ext.DI;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline.Ext.DI;

public class DINodeActivatorTests {
    private sealed class TestNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    private interface IService { }
    private sealed class Service : IService { }

    private sealed class DependentNode(IService service) : AbstractNode {
        public IService Service { get; } = service;
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    [Fact]
    public void CreateNode_Generic_ParameterlessNode_ReturnsCorrectType() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);

        var node = activator.CreateNode<TestNode>();

        Assert.NotNull(node);
        Assert.IsType<TestNode>(node);
    }

    [Fact]
    public void CreateNode_Generic_WithRegisteredDependency_InjectsService() {
        var services = new ServiceCollection();
        services.AddSingleton<IService, Service>();
        var provider = services.BuildServiceProvider();
        var activator = new DINodeActivator(provider);

        var node = activator.CreateNode<DependentNode>();

        Assert.NotNull(node);
        Assert.NotNull(node.Service);
        Assert.IsType<Service>(node.Service);
    }

    [Fact]
    public void CreateNode_Generic_WithMissingDependency_ThrowsInvalidOperationException() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);

        Assert.Throws<InvalidOperationException>(() => activator.CreateNode<DependentNode>());
    }

    [Fact]
    public void CreateNode_Type_WithValidAbstractNodeType_ReturnsCorrectType() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);

        var node = activator.CreateNode(typeof(TestNode));

        Assert.NotNull(node);
        Assert.IsType<TestNode>(node);
    }

    [Fact]
    public void CreateNode_Type_WithNonAbstractNodeType_ThrowsArgumentException() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);

        var ex = Assert.Throws<ArgumentException>(() => activator.CreateNode(typeof(List<int>)));
        Assert.Equal("type", ex.ParamName);
    }

    [Fact]
    public void CreateNode_Type_WithRegisteredDependency_InjectsService() {
        var services = new ServiceCollection();
        services.AddSingleton<IService, Service>();
        var provider = services.BuildServiceProvider();
        var activator = new DINodeActivator(provider);

        var node = activator.CreateNode(typeof(DependentNode));

        Assert.NotNull(node);
        Assert.IsType<DependentNode>(node);
        Assert.True(node is DependentNode dn && dn.Service is Service);
    }

    [Fact]
    public void CreateNode_Type_WithMissingDependency_ThrowsInvalidOperationException() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);

        Assert.Throws<InvalidOperationException>(() => activator.CreateNode(typeof(DependentNode)));
    }

    [Fact]
    public void CreateNode_Generic_CalledMultipleTimes_ReturnsNewInstances() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);

        var node1 = activator.CreateNode<TestNode>();
        var node2 = activator.CreateNode<TestNode>();

        Assert.NotSame(node1, node2);
    }
}
