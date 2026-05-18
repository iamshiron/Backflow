using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineBuilderTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) {
            return null;
        }
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;
        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class DestNode : AbstractNode {
        public readonly IInputPort<int> In;
        public DestNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class RelayNode : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;
        public RelayNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void AddNode_ReturnsInstanceWithCorrectId() {
        var builder = new PipelineBuilder(_registry);
        var node = new SourceNode();
        var instance = builder.AddNode(node);
        Assert.Contains("SourceNode", instance.ID);
        Assert.Same(node, instance.Node);
    }

    [Fact]
    public void AddNode_SameType_IncrementsId() {
        var builder = new PipelineBuilder(_registry);
        var a = builder.AddNode(new SourceNode());
        var b = builder.AddNode(new SourceNode());
        Assert.EndsWith("-0", a.ID);
        Assert.EndsWith("-1", b.ID);
    }

    [Fact]
    public void AddNode_DifferentTypes_StartsAtZero() {
        var builder = new PipelineBuilder(_registry);
        var src = builder.AddNode(new SourceNode());
        var dest = builder.AddNode(new DestNode());
        Assert.EndsWith("-0", src.ID);
        Assert.EndsWith("-0", dest.ID);
    }

    [Fact]
    public void AddNode_CreatesPortMappings() {
        var builder = new PipelineBuilder(_registry);
        var node = new SourceNode();
        var instance = builder.AddNode(node);
        Assert.Equal(node.Ports.Count, instance.Mappings.Count);
        foreach (var port in node.Ports) {
            Assert.True(instance.Mappings.ContainsKey(port));
            Assert.NotEqual(Guid.Empty, instance.Mappings[port]);
        }
    }

    [Fact]
    public void AddConnection_InvalidSourcePort_ThrowsInvalidPortException() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var destNode = new DestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        var fakePort = new OutputPort<int>("nonexistent");

        Assert.Throws<InvalidPortException>(() =>
            builder.AddConnection(src, fakePort, dest, destNode.In));
    }

    [Fact]
    public void AddConnection_InvalidDestPort_ThrowsInvalidPortException() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var destNode = new DestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        var fakePort = new InputPort<int>("nonexistent", 0, new PassValidator<int>());

        Assert.Throws<InvalidPortException>(() =>
            builder.AddConnection(src, srcNode.Out, dest, fakePort));
    }

    [Fact]
    public void AddConnection_Cycle_ThrowsPipelineCycleException() {
        var builder = new PipelineBuilder(_registry);
        var nodeA = new RelayNode();
        var nodeB = new RelayNode();

        var a = builder.AddNode(nodeA);
        var b = builder.AddNode(nodeB);

        builder.AddConnection(a, nodeA.Out, b, nodeB.In);

        Assert.Throws<PipelineCycleException>(() =>
            builder.AddConnection(b, nodeB.Out, a, nodeA.In));
    }

    [Fact]
    public void AddConnection_ValidPorts_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var destNode = new DestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);

        var exception = Record.Exception(() =>
            builder.AddConnection(src, srcNode.Out, dest, destNode.In));
        Assert.Null(exception);
    }

    [Fact]
    public void AddConnection_SharesChannelMapping() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var destNode = new DestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);

        builder.AddConnection(src, srcNode.Out, dest, destNode.In);

        Assert.Equal(src.Mappings[srcNode.Out], dest.Mappings[destNode.In]);
    }

    [Fact]
    public void Build_ReturnsPipelineWithTopology() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var destNode = new DestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        builder.AddConnection(src, srcNode.Out, dest, destNode.In);

        var pipeline = builder.Build();
        Assert.NotNull(pipeline.Topology);
        Assert.NotNull(pipeline.Edges);
        Assert.Single(pipeline.Edges);
    }

    [Fact]
    public void Build_NoConnections_ReturnsEmptyEdges() {
        var builder = new PipelineBuilder(_registry);
        builder.AddNode(new SourceNode());

        var pipeline = builder.Build();
        Assert.Empty(pipeline.Edges);
    }

    [Fact]
    public void AddConnection_EdgeRecordsCorrectPorts() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new SourceNode();
        var destNode = new DestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        builder.AddConnection(src, srcNode.Out, dest, destNode.In);

        var pipeline = builder.Build();
        var edge = pipeline.Edges[0];
        Assert.Equal(src, edge.SourceNode);
        Assert.Equal(dest, edge.DestinationNode);
        Assert.Equal(srcNode.Out, edge.SourcePort);
        Assert.Equal(destNode.In, edge.DestinationPort);
    }
}
