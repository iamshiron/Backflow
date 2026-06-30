using System.Text.Json;
using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Exceptions;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;
using Shiron.Backflow.Serialization;
using Xunit;

namespace Shiron.Backflow.Tests;

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

    private class TestGenericNode<T> : AbstractGenericNode {
        public readonly IInputPort<T> In;
        public readonly IOutputPort<T> Out;

        public TestGenericNode() {
            In = Input(new InputPort<T>("in", default!, new PassValidator<T>()));
            Out = Output(new OutputPort<T>("out"));
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
        _registry.RegisterGeneric(typeof(TestGenericNode<>));
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

        var ctx = ArrayPipelineContext.ForPipeline(pipeline);
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

        var ctx = ArrayPipelineContext.ForPipeline(pipeline);
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

        var ctx = ArrayPipelineContext.ForPipeline(pipeline);
        ctx.Write(inst, strNode.Out, "hello world");

        var json = pipeline.SerializeInputs(ctx);
        var restoredCtx = PipelineSerialization.DeserializeInputs(json, pipeline);

        Assert.Equal("hello world", restoredCtx.Read<string>(inst, strNode.Out));
    }

    [Fact]
    public void RoundTrip_SameNodeTypeConnected_PreservesTopology() {
        var builder = new PipelineBuilder(_registry);
        var relay1Node = new RelayNode();
        var relay2Node = new RelayNode();
        var relay1 = builder.AddNode(relay1Node);
        var relay2 = builder.AddNode(relay2Node);
        builder.AddConnection(relay1, relay1Node.Out, relay2, relay2Node.In);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, _registry);

        Assert.Equal(2, restored.Topology.Nodes.Count());
        Assert.Single(restored.Edges);
        Assert.NotEqual(restored.Edges[0].SourceNode.ID, restored.Edges[0].DestinationNode.ID);
    }

    [Fact]
    public void FromDefinitionDto_MissingNode_ThrowsPipelineDeserializationException() {
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("n1", "NonExistent.Node")],
            []
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.NodeResolution, ex.Errors[0].Phase);
        Assert.Equal("n1", ex.Errors[0].NodeId);
        Assert.Contains("NonExistent.Node", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_MissingGenericBlueprint_ThrowsPipelineDeserializationException() {
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("g1", "NonExistent.GenericNode`1", ["System.Int32"])],
            []
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.NodeResolution, ex.Errors[0].Phase);
        Assert.Equal("g1", ex.Errors[0].NodeId);
        Assert.Contains("NonExistent.GenericNode`1", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_MissingPort_ThrowsPipelineDeserializationException() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("s", typeof(SourceNode).FullName!),
                new NodeInstanceDto("d", typeof(DestNode).FullName!)
            ],
            [new EdgeDto("s", "nonexistent_port", "d", "in")]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.PortResolution, ex.Errors[0].Phase);
        Assert.Contains("nonexistent_port", ex.Errors[0].Message);
    }

    [Fact]
    public void DeserializeDefinition_InvalidJson_ThrowsPipelineDeserializationException() {
        var ex = Assert.Throws<PipelineDeserializationException>(() =>
            PipelineSerialization.DeserializeDefinition("not valid json", _registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.JsonParsing, ex.Errors[0].Phase);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public void DeserializeInputs_InvalidJson_ThrowsPipelineDeserializationException() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();

        var ex = Assert.Throws<PipelineDeserializationException>(() =>
            PipelineSerialization.DeserializeInputs("not valid json", pipeline));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.JsonParsing, ex.Errors[0].Phase);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public void FromInputs_MissingNode_ThrowsPipelineDeserializationException() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();

        var dto = new PipelineInputsDto(
            new Dictionary<string, Dictionary<string, InputDto>> {
                ["nonexistent-node"] = new() { ["port"] = new InputDto(42, typeof(int).FullName!) }
            }
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromInputs(pipeline));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.NodeResolution, ex.Errors[0].Phase);
        Assert.Contains("nonexistent-node", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_MultipleMissingNodes_AccumulatesAllErrors() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("n1", "NonExistent.A"),
                new NodeInstanceDto("n2", "NonExistent.B"),
                new NodeInstanceDto("n3", "NonExistent.C"),
            ],
            []
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Equal(3, ex.Errors.Count);
        Assert.All(ex.Errors, e => Assert.Equal(DeserializationPhase.NodeResolution, e.Phase));
        Assert.Contains(ex.Errors, e => e.NodeId == "n1");
        Assert.Contains(ex.Errors, e => e.NodeId == "n2");
        Assert.Contains(ex.Errors, e => e.NodeId == "n3");
    }

    [Fact]
    public void FromDefinitionDto_MultipleMissingPorts_AccumulatesAllErrors() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("s1", typeof(SourceNode).FullName!),
                new NodeInstanceDto("s2", typeof(SourceNode).FullName!),
                new NodeInstanceDto("d1", typeof(DestNode).FullName!),
                new NodeInstanceDto("d2", typeof(DestNode).FullName!),
            ],
            [
                new EdgeDto("s1", "bad_port_1", "d1", "in"),
                new EdgeDto("s2", "bad_port_2", "d2", "in"),
            ]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Equal(2, ex.Errors.Count);
        Assert.All(ex.Errors, e => Assert.Equal(DeserializationPhase.PortResolution, e.Phase));
        Assert.Contains(ex.Errors, e => e.Message.Contains("bad_port_1"));
        Assert.Contains(ex.Errors, e => e.Message.Contains("bad_port_2"));
    }

    [Fact]
    public void FromDefinitionDto_MixedErrors_AccumulatesAllPhases() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("good_src", typeof(SourceNode).FullName!),
                new NodeInstanceDto("missing_type", "NonExistent.Node"),
                new NodeInstanceDto("good_dst", typeof(DestNode).FullName!),
            ],
            [
                new EdgeDto("good_src", "nonexistent_port", "good_dst", "in"),
            ]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains(ex.Errors, e => e.Phase == DeserializationPhase.NodeResolution);
        Assert.Contains(ex.Errors, e => e.Phase == DeserializationPhase.PortResolution);
    }

    [Fact]
    public void FromDefinitionDto_DuplicateNodeIds_ReportsDuplicate() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("dup", typeof(SourceNode).FullName!),
                new NodeInstanceDto("dup", typeof(SourceNode).FullName!),
            ],
            []
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.Structure, ex.Errors[0].Phase);
        Assert.Contains("Duplicate", ex.Errors[0].Message);
        Assert.Contains("dup", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_DanglingEdgeReference_ReportsUnknownNode() {
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("n1", typeof(SourceNode).FullName!)],
            [new EdgeDto("n1", "out", "ghost", "in")]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.Structure, ex.Errors[0].Phase);
        Assert.Contains("ghost", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_SelfLoop_ReportsCycle() {
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("n1", typeof(RelayNode).FullName!)],
            [new EdgeDto("n1", "out", "n1", "in")]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.Graph, ex.Errors[0].Phase);
        Assert.Contains("Self-loop", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_TwoNodeCycle_ReportsCycle() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("a", typeof(RelayNode).FullName!),
                new NodeInstanceDto("b", typeof(RelayNode).FullName!),
            ],
            [
                new EdgeDto("a", "out", "b", "in"),
                new EdgeDto("b", "out", "a", "in"),
            ]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.Graph, ex.Errors[0].Phase);
        Assert.Contains("cycle", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_MissingSourcePort_IncludesAvailablePortsHint() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("s", typeof(SourceNode).FullName!),
                new NodeInstanceDto("d", typeof(DestNode).FullName!),
            ],
            [new EdgeDto("s", "nonexistent_port", "d", "in")]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Contains("Available ports: [out]", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_MissingDestinationPort_IncludesAvailablePortsHint() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("s", typeof(SourceNode).FullName!),
                new NodeInstanceDto("d", typeof(DestNode).FullName!),
            ],
            [new EdgeDto("s", "out", "d", "nonexistent_port")]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Contains("Available ports: [in, out]", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_PortError_IncludesEdgeIndexContext() {
        var dto = new PipelineDefinitionDto(
            [
                new NodeInstanceDto("s", typeof(SourceNode).FullName!),
                new NodeInstanceDto("d", typeof(DestNode).FullName!),
            ],
            [new EdgeDto("s", "bad_port", "d", "in")]
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(0, ex.Errors[0].EdgeIndex);
    }

    [Fact]
    public void FromDefinitionDto_MissingGenericBlueprint_IncludesTypeArgsHint() {
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("g1", "NonExistent.GenericNode`1", ["System.Int32", "System.String"])],
            []
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Contains("Type arguments: [System.Int32, System.String]", ex.Errors[0].Message);
    }

    [Fact]
    public void FromInputs_MissingPort_IncludesAvailablePortsHint() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();
        var nodeId = pipeline.Topology.Nodes.First().ID;

        var dto = new PipelineInputsDto(
            new Dictionary<string, Dictionary<string, InputDto>> {
                [nodeId] = new() { ["nonexistent"] = new InputDto(42, typeof(int).FullName!) }
            }
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromInputs(pipeline));
        Assert.Single(ex.Errors);
        Assert.Contains("Available ports: [out]", ex.Errors[0].Message);
    }

    [Fact]
    public void FromDefinitionDto_UnresolvableGenericTypeArg_ReportsTypeResolutionError() {
        var openTypeName = typeof(TestGenericNode<>).FullName!;
        var dto = new PipelineDefinitionDto(
            [new NodeInstanceDto("g1", openTypeName, ["Totally.Fake.Type"])],
            []
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromDefinitionDto(_registry));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.TypeResolution, ex.Errors[0].Phase);
        Assert.Contains("Totally.Fake.Type", ex.Errors[0].Message);
    }

    [Fact]
    public void FromInputs_MultipleMissingPorts_AccumulatesAllErrors() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();

        var dto = new PipelineInputsDto(
            new Dictionary<string, Dictionary<string, InputDto>> {
                [pipeline.Topology.Nodes.First().ID] = new() {
                    ["bad_port_1"] = new InputDto(1, typeof(int).FullName!),
                    ["bad_port_2"] = new InputDto(2, typeof(int).FullName!),
                }
            }
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromInputs(pipeline));
        Assert.Equal(2, ex.Errors.Count);
        Assert.All(ex.Errors, e => Assert.Equal(DeserializationPhase.PortResolution, e.Phase));
    }

    [Fact]
    public void FromInputs_UnresolvableType_ReportsTypeResolutionError() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());
        var pipeline = builder.Build();
        var nodeId = pipeline.Topology.Nodes.First().ID;

        var dto = new PipelineInputsDto(
            new Dictionary<string, Dictionary<string, InputDto>> {
                [nodeId] = new() { ["out"] = new InputDto(42, "Totally.Fake.Type") }
            }
        );

        var ex = Assert.Throws<PipelineDeserializationException>(() => dto.FromInputs(pipeline));
        Assert.Single(ex.Errors);
        Assert.Equal(DeserializationPhase.TypeResolution, ex.Errors[0].Phase);
    }

    [Fact]
    public void RoundTrip_ConnectedPorts_ShareChannel() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, _registry);

        var edge = Assert.Single(restored.Edges);
        var sourceChannel = edge.SourceNode.Mappings[edge.SourcePort];
        var destChannel = edge.DestinationNode.Mappings[edge.DestinationPort];

        Assert.Equal(sourceChannel, destChannel);
    }

    [Fact]
    public void RoundTrip_ReconstructedChannels_CarryValues() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var dstNode = new DestNode();
        var source = builder.AddNode(srcNode);
        var dest = builder.AddNode(dstNode);
        builder.AddConnection(source, srcNode.Out, dest, dstNode.In);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, _registry);

        var edge = Assert.Single(restored.Edges);
        var ctx = ArrayPipelineContext.ForPipeline(restored);

        ctx.Write(edge.SourceNode, edge.SourcePort, 12345);

        Assert.Equal(12345, ctx.Read<int>(edge.DestinationNode, edge.DestinationPort));
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
