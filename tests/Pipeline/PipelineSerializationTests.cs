using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Serialization;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineSerializationTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;

        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class DestNode : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;

        public DestNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class RelayNode : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;

        public RelayNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class StringNode : AbstractNode {
        public readonly IOutputPort<string> Out;

        public StringNode() {
            Out = Output(new OutputPort<string>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private readonly NodeRegistry _registry = new();

    public PipelineSerializationTests() {
        _registry.Register<SourceNode>();
        _registry.Register<DestNode>();
        _registry.Register<RelayNode>();
        _registry.Register<StringNode>();
    }

    [Fact]
    public void ToDefinitionDto_SingleNode_ContainsCorrectNodeType() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();

        var dto = pipeline.ToDefinitionDto();

        Assert.Single(dto.Nodes);
        Assert.Contains("SourceNode", dto.Nodes[0].NodeTypeName);
        Assert.Empty(dto.Edges);
    }

    [Fact]
    public void ToDefinitionDto_TwoConnectedNodes_ContainsEdge() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var dto = pipeline.ToDefinitionDto();

        Assert.Equal(2, dto.Nodes.Length);
        Assert.Single(dto.Edges);
        Assert.Contains("SourceNode", dto.Edges[0].SourceNodeId);
        Assert.Contains("DestNode", dto.Edges[0].DestinationNodeId);
        Assert.Equal("out", dto.Edges[0].SourcePortName);
        Assert.Equal("in", dto.Edges[0].DestinationPortName);
    }

    [Fact]
    public void ToDefinitionDto_ThreeNodeChain_ContainsTwoEdges() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var relayNode = new RelayNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var relay = builder.AddNode(relayNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, relay, relayNode.In);
        builder.AddConnection(relay, relayNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var dto = pipeline.ToDefinitionDto();

        Assert.Equal(3, dto.Nodes.Length);
        Assert.Equal(2, dto.Edges.Length);
    }

    [Fact]
    public void ToInputsDto_CapturesPortValues() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var ctx = new PipelineContext();
        ctx.Write(source, srcNode.Out, 42);

        var dto = pipeline.ToInputsDto(ctx);

        Assert.True(dto.Inputs.Count > 0);
    }

    [Fact]
    public void RoundTrip_Definition_PreservesNodeCount() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, _registry);

        Assert.Equal(pipeline.Topology.Nodes.Count(), restored.Topology.Nodes.Count());
    }

    [Fact]
    public void RoundTrip_Definition_PreservesEdgeCount() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, _registry);

        Assert.Equal(pipeline.Edges.Length, restored.Edges.Length);
    }

    [Fact]
    public void RoundTrip_Inputs_PreservesValues() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var source = builder.AddNode(srcNode);
        var pipeline = builder.Build();

        var ctx = new PipelineContext();
        ctx.Write(source, srcNode.Out, 99);

        var json = pipeline.SerializeInputs(ctx);
        var restoredCtx = PipelineSerialization.DeserializeInputs(json, pipeline);

        Assert.Equal(99, restoredCtx.Read<int>(source, srcNode.Out));
    }

    [Fact]
    public void RoundTrip_Inputs_StringValue_PreservesValue() {
        var builder = new PipelineBuilder(_registry);
        var strNode = new StringNode();
        var inst = builder.AddNode(strNode);
        var pipeline = builder.Build();

        var ctx = new PipelineContext();
        ctx.Write(inst, strNode.Out, "hello world");

        var json = pipeline.SerializeInputs(ctx);
        var restoredCtx = PipelineSerialization.DeserializeInputs(json, pipeline);

        Assert.Equal("hello world", restoredCtx.Read<string>(inst, strNode.Out));
    }

    [Fact]
    public void FromDefinitionDto_MissingNode_ThrowsInvalidOperationException() {
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("n1", "NonExistent.Node", [])],
            []
        );

        Assert.Throws<InvalidOperationException>(() => dto.FromDefinitionDto(_registry));
    }

    [Fact]
    public void FromDefinitionDto_MissingPort_ThrowsInvalidOperationException() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("s", typeof(SourceNode).FullName!, []),
                new NodeInstanceDto("d", typeof(DestNode).FullName!, [])
            ],
            [new EdgeDto("s", "nonexistent_port", "d", "in")]
        );

        Assert.Throws<InvalidOperationException>(() => dto.FromDefinitionDto(_registry));
    }

    [Fact]
    public void DeserializeDefinition_InvalidJson_ThrowsJsonException() {
        Assert.Throws<JsonException>(() =>
            PipelineSerialization.DeserializeDefinition("not valid json", _registry));
    }

    [Fact]
    public void DeserializeInputs_InvalidJson_ThrowsJsonException() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();

        Assert.Throws<JsonException>(() =>
            PipelineSerialization.DeserializeInputs("not valid json", pipeline));
    }

    [Fact]
    public void FromInputs_MissingNode_ThrowsInvalidOperationException() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();

        var dto = new PipelineInputsDto(
            new Dictionary<string, Dictionary<string, InputDto>> {
                ["nonexistent-node"] = new() { ["port"] = new InputDto(42, typeof(int).FullName!) }
            }
        );

        Assert.Throws<InvalidOperationException>(() => dto.FromInputs(pipeline));
    }

    [Fact]
    public void RoundTrip_PortMappings_Preserved() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, _registry);

        var restoredDest = restored.Topology.Nodes.First(n => n.ID.Contains("DestNode"));
        var restoredSource = restored.Topology.Nodes.First(n => n.ID.Contains("SourceNode"));

        var destInGuid = restoredDest.Mappings.Values.First();
        var sourceOutGuid = restoredSource.Mappings.Values.First();

        Assert.Equal(destInGuid, sourceOutGuid);
    }
}

public class PipelineSerializationArrayTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class PassAllArrayValidator : IPortValidator<int[]> {
        public string? Validate(int[]? value) => null;
    }

    private class ArrayDestNode : AbstractNode {
        public readonly IArrayInputPort<int> Values;

        public ArrayDestNode() {
            Values = Input(new ArrayInputPort<int>(
                "values", 0, new PassValidator<int>(), new PassAllArrayValidator(), 0, null
            ));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;

        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    [Fact]
    public void RoundTrip_ArrayEdge_PreservesDestIndex() {
        var registry = new NodeRegistry();
        registry.Register<SourceNode>();
        registry.Register<ArrayDestNode>();

        var builder = new PipelineBuilder(registry);
        var s1Node = new SourceNode();
        var s2Node = new SourceNode();
        var destNode = new ArrayDestNode();
        var s1 = builder.AddNode(s1Node);
        var s2 = builder.AddNode(s2Node);
        var dest = builder.AddNode(destNode);
        builder.AddConnection(s1, s1Node.Out, dest, (IPort) destNode.Values, 0);
        builder.AddConnection(s2, s2Node.Out, dest, (IPort) destNode.Values, 1);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, registry);

        Assert.Equal(2, restored.Edges.Length);
        Assert.NotNull(restored.Edges[0].DestIndex);
        Assert.NotNull(restored.Edges[1].DestIndex);
    }
}
