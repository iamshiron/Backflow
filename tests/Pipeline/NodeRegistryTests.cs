using Shiron.Lib.Pipeline;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class NodeRegistryTests {
    [Fact]
    public void Register_Generic_RegistersAndReturnsNode() {
        var registry = new NodeRegistry();
        var node = registry.Register<AddNode>();

        Assert.NotNull(node);
        Assert.IsType<AddNode>(node);
        Assert.Same(node, registry.Get<AddNode>());
    }

    [Fact]
    public void Register_Instance_RegistersNode() {
        var registry = new NodeRegistry();
        var node = new AddNode();
        registry.Register(node);

        Assert.Same(node, registry.Get<AddNode>());
    }

    [Fact]
    public void Register_SameTypeTwice_Throws() {
        var registry = new NodeRegistry();
        registry.Register<AddNode>();

        Assert.Throws<ArgumentException>(() => registry.Register(new AddNode()));
    }

    [Fact]
    public void Register_GenericSameTypeTwice_Throws() {
        var registry = new NodeRegistry();
        registry.Register<AddNode>();

        Assert.Throws<ArgumentException>(() => registry.Register<AddNode>());
    }

    [Fact]
    public void Get_UnregisteredType_ReturnsNull() {
        var registry = new NodeRegistry();

        Assert.Null(registry.Get<AddNode>());
    }

    [Fact]
    public void Get_ByType_ReturnsCorrectNode() {
        var registry = new NodeRegistry();
        var addNode = registry.Register<AddNode>();
        var subNode = registry.Register<SubtractNode>();

        Assert.Same(addNode, registry.Get(typeof(AddNode)));
        Assert.Same(subNode, registry.Get(typeof(SubtractNode)));
    }

    [Fact]
    public void Register_Generic_CreatesNewInstance() {
        var registry = new NodeRegistry();
        var node = registry.Register<AddNode>();

        Assert.NotNull(node);
        Assert.NotEmpty(node.Inputs);
        Assert.NotEmpty(node.Outputs);
    }

    [Fact]
    public void Register_MultipleDifferentTypes_AllAccessible() {
        var registry = new NodeRegistry();
        var addNode = registry.Register<AddNode>();
        var subNode = registry.Register<SubtractNode>();
        var mulNode = registry.Register<MultiplyNode>();

        Assert.Same(addNode, registry.Get<AddNode>());
        Assert.Same(subNode, registry.Get<SubtractNode>());
        Assert.Same(mulNode, registry.Get<MultiplyNode>());
    }

    [Fact]
    public void GetByFullName_ReturnsCorrectNode() {
        var registry = new NodeRegistry();
        var addNode = registry.Register<AddNode>();

        var result = registry.GetByFullName(typeof(AddNode).FullName!);

        Assert.Same(addNode, result);
    }

    [Fact]
    public void GetByFullName_UnregisteredType_ReturnsNull() {
        var registry = new NodeRegistry();

        Assert.Null(registry.GetByFullName(typeof(AddNode).FullName!));
    }

    [Fact]
    public void GetByFullName_MultipleTypes_ReturnsCorrectNode() {
        var registry = new NodeRegistry();
        var addNode = registry.Register<AddNode>();
        var subNode = registry.Register<SubtractNode>();

        Assert.Same(addNode, registry.GetByFullName(typeof(AddNode).FullName!));
        Assert.Same(subNode, registry.GetByFullName(typeof(SubtractNode).FullName!));
    }

    [Fact]
    public void GetByFullName_UsesFullNameNotName() {
        var registry = new NodeRegistry();
        registry.Register<AddNode>();

        Assert.Null(registry.GetByFullName(nameof(AddNode)));
    }
}
