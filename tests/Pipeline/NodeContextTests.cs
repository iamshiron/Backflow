using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class NodeContextTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SimpleNode : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<string> Out;

        public SimpleNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<string>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var val = In.Read(context);
            Out.Write(context, val.ToString());
            return ValueTask.FromResult(true);
        }
    }

    private static (PipelineBuilder.NodeInstance instance, SimpleNode node, PipelineContext pipelineCtx, NodeContext nodeCtx)
        CreateNodeWithMappings() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new SimpleNode();
        var instance = builder.AddNode(node);
        var pipelineCtx = new PipelineContext();
        var nodeCtx = new NodeContext(pipelineCtx, instance.Mappings);
        return (instance, node, pipelineCtx, nodeCtx);
    }

    [Fact]
    public void Write_DelegatesToPipelineContextViaMapping() {
        var (instance, node, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        nodeCtx.Write(node.Out, "hello");

        var guid = instance.Mappings[node.Out];
        Assert.Equal("hello", pipelineCtx.Read<string>(guid));
    }

    [Fact]
    public void Read_DelegatesToPipelineContextViaMapping() {
        var (instance, node, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        var guid = instance.Mappings[node.In];
        pipelineCtx.Write(guid, 42);

        Assert.Equal(42, nodeCtx.Read<int>(node.In));
    }

    [Fact]
    public void Read_MissingPort_ReturnsDefault() {
        var (_, node, _, nodeCtx) = CreateNodeWithMappings();

        Assert.Equal(0, nodeCtx.Read<int>(node.In));
    }

    [Fact]
    public void Write_ObjectOverload_DelegatesCorrectly() {
        var (instance, node, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        nodeCtx.Write(node.Out, (object?) "world");

        var guid = instance.Mappings[node.Out];
        Assert.Equal("world", pipelineCtx.ReadAny(guid));
    }

    [Fact]
    public void ReadAny_DelegatesToPipelineContext() {
        var (instance, node, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        var guid = instance.Mappings[node.In];
        pipelineCtx.Write(guid, 99);

        Assert.Equal(99, nodeCtx.ReadAny(node.In));
    }

    [Fact]
    public void Has_ExistingValue_ReturnsTrue() {
        var (instance, node, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        var guid = instance.Mappings[node.In];
        pipelineCtx.Write(guid, 10);

        Assert.True(nodeCtx.Has<int>(node.In));
    }

    [Fact]
    public void Has_MissingValue_ReturnsFalse() {
        var (_, node, _, nodeCtx) = CreateNodeWithMappings();

        Assert.False(nodeCtx.Has<int>(node.In));
    }

    [Fact]
    public void HasAny_ExistingValue_ReturnsTrue() {
        var (instance, node, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        var guid = instance.Mappings[node.In];
        pipelineCtx.Write(guid, 10);

        Assert.True(nodeCtx.HasAny(node.In));
    }

    [Fact]
    public void HasAny_MissingValue_ReturnsFalse() {
        var (_, node, _, nodeCtx) = CreateNodeWithMappings();

        Assert.False(nodeCtx.HasAny(node.In));
    }

    [Fact]
    public void InitializeArray_WithCount_AssemblesIndexedInputs() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);

        var node = new ArrayConsumerTestNode();
        var instance = builder.AddNode(node);
        var pipelineCtx = new PipelineContext();

        var sourceGuid1 = Guid.NewGuid();
        var sourceGuid2 = Guid.NewGuid();
        pipelineCtx.Write(sourceGuid1, 10);
        pipelineCtx.Write(sourceGuid2, 20);

        var arrayPort = (IPort) node.Values;
        var indexedInputs = new Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>> {
            [arrayPort] = new List<(int Index, Guid SourceGuid)> {
                (0, sourceGuid1), (1, sourceGuid2)
            }
        };

        var nodeCtx = new NodeContext(pipelineCtx, instance.Mappings, indexedInputs);

        nodeCtx.InitializeArray(node.Values, 2);

        Assert.True(nodeCtx.Has<int[]>(node.Values));
        var array = nodeCtx.Read<int[]>(node.Values);
        Assert.Equal([10, 20], array!);
    }

    [Fact]
    public void InitializeArray_WithoutCount_InfersFromMaxIndex() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);

        var node = new ArrayConsumerTestNode();
        var instance = builder.AddNode(node);
        var pipelineCtx = new PipelineContext();

        var sourceGuid = Guid.NewGuid();
        pipelineCtx.Write(sourceGuid, 42);

        var arrayPort = (IPort) node.Values;
        var indexedInputs = new Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>> {
            [arrayPort] = new List<(int Index, Guid SourceGuid)> {
                (2, sourceGuid)
            }
        };

        var nodeCtx = new NodeContext(pipelineCtx, instance.Mappings, indexedInputs);

        nodeCtx.InitializeArray(node.Values);

        var array = nodeCtx.Read<int[]>(node.Values);
        Assert.NotNull(array);
        Assert.Equal(3, array.Length);
        Assert.Equal(42, array[2]);
    }

    [Fact]
    public void InitializeArray_WithoutIndexedConnections_ThrowsInvalidOperationException() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);

        var node = new ArrayConsumerTestNode();
        var instance = builder.AddNode(node);
        var pipelineCtx = new PipelineContext();

        var nodeCtx = new NodeContext(pipelineCtx, instance.Mappings);

        Assert.Throws<InvalidOperationException>(() => nodeCtx.InitializeArray(node.Values));
    }

    [Fact]
    public void InitializeArray_WithCount_NoSources_CreatesDefaultArray() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);

        var node = new ArrayConsumerTestNode();
        var instance = builder.AddNode(node);
        var pipelineCtx = new PipelineContext();

        var nodeCtx = new NodeContext(pipelineCtx, instance.Mappings);

        nodeCtx.InitializeArray(node.Values, 3);

        var array = nodeCtx.Read<int[]>(node.Values);
        Assert.NotNull(array);
        Assert.Equal(3, array.Length);
        Assert.All(array, v => Assert.Equal(0, v));
    }

    private class ArrayConsumerTestNode : AbstractNode {
        public readonly IArrayInputPort<int> Values;

        public ArrayConsumerTestNode() {
            Values = Input(new ArrayInputPort<int>(
                "values",
                0,
                new PassValidator<int>(),
                new PassAllArrayValidator(),
                0, null
            ));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return ValueTask.FromResult(true);
        }
    }

    [Fact]
    public void Services_ReturnsInnerContextServices() {
        var (_, _, pipelineCtx, nodeCtx) = CreateNodeWithMappings();

        Assert.Same(pipelineCtx.Services, nodeCtx.Services);
    }

    [Fact]
    public void Services_WithCustomServiceProvider_ReturnsProvider() {
        var serviceProvider = new TestServiceProvider();
        var pipelineCtx = new PipelineContext(serviceProvider);
        var mappings = new Dictionary<IPort, Guid>();
        var nodeCtx = new NodeContext(pipelineCtx, mappings);

        Assert.Same(serviceProvider, nodeCtx.Services);
    }

    private class TestServiceProvider : IServiceProvider {
        public object? GetService(Type serviceType) => null;
    }

    private class PassAllArrayValidator : IPortValidator<int[]> {
        public string? Validate(int[]? value) => null;
    }
}
