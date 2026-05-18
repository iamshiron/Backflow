using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class NodeRegistryExtendedTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class StubNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class AnotherStubNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class GenericStubNode<T> : AbstractGenericNode {
        public readonly IInputPort<T> In;

        public GenericStubNode() {
            In = Input(new InputPort<T>("in", default, new PassValidator<T>()));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    [Fact]
    public void RegisterGeneric_NonGenericType_ThrowsArgumentException() {
        var registry = new NodeRegistry();

        Assert.Throws<ArgumentException>(() => registry.RegisterGeneric(typeof(StubNode)));
    }

    [Fact]
    public void RegisterGeneric_NonAbstractGenericNode_ThrowsArgumentException() {
        var registry = new NodeRegistry();

        Assert.Throws<ArgumentException>(() =>
            registry.RegisterGeneric(typeof(List<>)));
    }

    [Fact]
    public void RegisterGeneric_DuplicateRegistration_ThrowsInvalidOperationException() {
        var registry = new NodeRegistry();
        registry.RegisterGeneric(typeof(GenericStubNode<>));

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterGeneric(typeof(GenericStubNode<>)));
    }

    [Fact]
    public void GetOrCreateConcrete_SameTypeArgs_ReturnsSameInstance() {
        var registry = new NodeRegistry();

        var node1 = registry.GetOrCreateConcrete(typeof(GenericStubNode<>), [typeof(int)]);
        var node2 = registry.GetOrCreateConcrete(typeof(GenericStubNode<>), [typeof(int)]);

        Assert.Same(node1, node2);
    }

    [Fact]
    public void GetOrCreateConcrete_DifferentTypeArgs_ReturnsDifferentInstances() {
        var registry = new NodeRegistry();

        var nodeInt = registry.GetOrCreateConcrete(typeof(GenericStubNode<>), [typeof(int)]);
        var nodeString = registry.GetOrCreateConcrete(typeof(GenericStubNode<>), [typeof(string)]);

        Assert.NotSame(nodeInt, nodeString);
    }

    [Fact]
    public void GetOrCreateConcrete_CreatedNodeHasCorrectPorts() {
        var registry = new NodeRegistry();

        var node = registry.GetOrCreateConcrete(typeof(GenericStubNode<>), [typeof(int)]);

        Assert.Single(node.Inputs);
        Assert.Equal("in", node.Inputs[0].Name);
        Assert.Equal(typeof(int), node.Inputs[0].PortType);
    }

    [Fact]
    public void GetOrCreateConcrete_DoesNotAffectExplicitRegister() {
        var registry = new NodeRegistry();
        registry.Register<StubNode>();

        var fromRegister = registry.Get<StubNode>();
        var fromExplicit = registry.Get(typeof(StubNode));

        Assert.Same(fromRegister, fromExplicit);
    }

    [Fact]
    public void GetByFullName_ReturnsNullForUnregistered() {
        var registry = new NodeRegistry();

        Assert.Null(registry.GetByFullName("NonExistent.Node"));
    }

    [Fact]
    public void GetBlueprint_ByType_ReturnsBlueprintForRegisteredGeneric() {
        var registry = new NodeRegistry();
        var bp = registry.RegisterGeneric(typeof(GenericStubNode<>));

        var retrieved = registry.GetBlueprint(typeof(GenericStubNode<>));

        Assert.NotNull(retrieved);
        Assert.Same(bp, retrieved);
    }

    [Fact]
    public void GetBlueprint_ByType_Unregistered_ReturnsNull() {
        var registry = new NodeRegistry();

        Assert.Null(registry.GetBlueprint(typeof(GenericStubNode<>)));
    }

    [Fact]
    public void GetAllBlueprints_ReturnsEmptyForNoGenericRegistrations() {
        var registry = new NodeRegistry();

        Assert.Empty(registry.GetAllBlueprints());
    }

    [Fact]
    public void GetAllBlueprints_ReturnsAllRegisteredBlueprints() {
        var registry = new NodeRegistry();
        registry.RegisterGeneric(typeof(GenericStubNode<>));

        var blueprints = registry.GetAllBlueprints().ToList();

        Assert.Single(blueprints);
    }

    [Fact]
    public void Get_GenericOverload_ReturnsNullForUnregistered() {
        var registry = new NodeRegistry();

        Assert.Null(registry.Get<StubNode>());
    }

    [Fact]
    public void Get_NonGenericOverload_ReturnsNullForUnregistered() {
        var registry = new NodeRegistry();

        Assert.Null(registry.Get(typeof(StubNode)));
    }

    [Fact]
    public void Register_Instance_SetsUpFullNameLookup() {
        var registry = new NodeRegistry();
        var node = new StubNode();
        registry.Register(node);

        var fullName = node.GetType().FullName!;
        Assert.Same(node, registry.GetByFullName(fullName));
    }
}
