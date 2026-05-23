using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Ext.DI;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
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

    private interface IGenericService { string Label { get; } }
    private sealed class GenericService : IGenericService {
        public string Label => "injected";
    }

    private sealed class DependentGenericNode<T> : AbstractGenericNode {
        public IGenericService Service { get; }
        public IInputPort<T> In { get; }
        public IOutputPort<T> Out { get; }

        public DependentGenericNode(IGenericService service) {
            Service = service;
            In = Input(new InputPort<T>("in", default, new PassValidator<T>()));
            Out = Output(new OutputPort<T>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
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

    [Fact]
    public void CreateNode_Generic_WithClosedGenericType_InjectsService() {
        var services = new ServiceCollection();
        services.AddSingleton<IGenericService, GenericService>();
        var provider = services.BuildServiceProvider();
        var activator = new DINodeActivator(provider);

        var node = activator.CreateNode(typeof(DependentGenericNode<int>));

        Assert.NotNull(node);
        var typed = Assert.IsType<DependentGenericNode<int>>(node);
        Assert.NotNull(typed.Service);
        Assert.IsType<GenericService>(typed.Service);
        Assert.Equal("injected", typed.Service.Label);
    }

    [Fact]
    public void GetOrCreateConcrete_WithDIActivator_InjectsDependencies() {
        var services = new ServiceCollection();
        services.AddSingleton<IGenericService, GenericService>();
        var provider = services.BuildServiceProvider();
        var activator = new DINodeActivator(provider);

        var registry = new NodeRegistry(activator);

        var node = registry.GetOrCreateConcrete(typeof(DependentGenericNode<>), [typeof(double)]);

        Assert.NotNull(node);
        var typed = Assert.IsType<DependentGenericNode<double>>(node);
        Assert.NotNull(typed.Service);
        Assert.Equal("injected", typed.Service.Label);
    }

    [Fact]
    public void GetOrCreateConcrete_WithDIActivator_CachesInstance() {
        var services = new ServiceCollection();
        services.AddSingleton<IGenericService, GenericService>();
        var provider = services.BuildServiceProvider();
        var activator = new DINodeActivator(provider);

        var registry = new NodeRegistry(activator);

        var node1 = registry.GetOrCreateConcrete(typeof(DependentGenericNode<>), [typeof(int)]);
        var node2 = registry.GetOrCreateConcrete(typeof(DependentGenericNode<>), [typeof(int)]);

        Assert.Same(node1, node2);
    }

    private sealed class DisposableNode : AbstractNode, IDisposable {
        public bool IsDisposed { get; private set; }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public void Dispose_DisposesTrackedDisposableNodes() {
        var services = new ServiceCollection().BuildServiceProvider();
        using var activator = new DINodeActivator(services);

        var node = activator.CreateNode<DisposableNode>();
        Assert.False(node.IsDisposed);

        activator.Dispose();

        Assert.True(node.IsDisposed);
    }

    [Fact]
    public void Dispose_DisposesAllTrackedNodes() {
        var services = new ServiceCollection().BuildServiceProvider();
        using var activator = new DINodeActivator(services);

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
        var services = new ServiceCollection().BuildServiceProvider();
        using var activator = new DINodeActivator(services);

        var node = activator.CreateNode<TestNode>();

        activator.Dispose();
    }

    [Fact]
    public void Dispose_DoubleDisposeDoesNotThrow() {
        var services = new ServiceCollection().BuildServiceProvider();
        var activator = new DINodeActivator(services);
        activator.CreateNode<TestNode>();

        activator.Dispose();
        activator.Dispose();
    }
}
