using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineBuilderTests {
    private PipelineBuilder CreateBuilder() => new(new NodeRegistry());

    [Fact]
    public void AddNode_ReturnsUniqueInstances() {
        var builder = CreateBuilder();
        var node = new AddNode();

        var instance1 = builder.AddNode(node);
        var instance2 = builder.AddNode(node);

        Assert.NotEqual(instance1.ID, instance2.ID);
    }

    [Fact]
    public void AddNode_AssignsUniquePortMappings() {
        var builder = CreateBuilder();
        var node = new AddNode();
        var instance = builder.AddNode(node);

        var mappingIds = instance.Mappings.Values.ToList();
        var uniqueIds = mappingIds.Distinct().ToList();

        Assert.Equal(node.Ports.Count(), mappingIds.Count);
        Assert.Equal(mappingIds.Count, uniqueIds.Count);
    }

    [Fact]
    public void AddNode_DifferentInstances_HaveDifferentMappings() {
        var builder = CreateBuilder();
        var node = new AddNode();

        var instance1 = builder.AddNode(node);
        var instance2 = builder.AddNode(node);

        foreach (var port in node.Ports) {
            Assert.NotEqual(instance1.Mappings[port], instance2.Mappings[port]);
        }
    }

    [Fact]
    public void AddConnection_CreatesEdge() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        var pipeline = builder.Build();
        Assert.Single(pipeline.Edges);
    }

    [Fact]
    public void AddConnection_MapsDestinationInputToSourceOutputMapping() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        Assert.Equal(addInst.Mappings[addNode.Sum], subInst.Mappings[subNode.Number2]);
    }

    [Fact]
    public void AddConnection_FanOut_OneOutputToMultipleInputs() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode1 = new SubtractNode();
        var subNode2 = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(subNode1);
        var subInst2 = builder.AddNode(subNode2);

        builder.AddConnection(addInst, addNode.Sum, subInst1, subNode1.Number2);
        builder.AddConnection(addInst, addNode.Sum, subInst2, subNode2.Number2);

        Assert.Equal(addInst.Mappings[addNode.Sum], subInst1.Mappings[subNode1.Number2]);
        Assert.Equal(addInst.Mappings[addNode.Sum], subInst2.Mappings[subNode2.Number2]);

        var pipeline = builder.Build();
        Assert.Equal(2, pipeline.Edges.Length);
    }

    [Fact]
    public void AddConnection_OverwritingInputMapping_LastConnectionWins() {
        var builder = CreateBuilder();
        var addNode1 = new AddNode();
        var addNode2 = new AddNode();
        var subNode = new SubtractNode();

        var addInst1 = builder.AddNode(addNode1);
        var addInst2 = builder.AddNode(addNode2);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst1, addNode1.Sum, subInst, subNode.Number2);
        builder.AddConnection(addInst2, addNode2.Sum, subInst, subNode.Number2);

        Assert.Equal(addInst2.Mappings[addNode2.Sum], subInst.Mappings[subNode.Number2]);
        Assert.NotEqual(addInst1.Mappings[addNode1.Sum], subInst.Mappings[subNode.Number2]);
    }

    [Fact]
    public void AddConnection_Cycle_Throws() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddConnection(subInst, subNode.Diff, addInst, addNode.Number1)
        );
    }

    [Fact]
    public void AddConnection_TransitiveCycle_Throws() {
        var builder = CreateBuilder();
        var node1 = new PassThroughNode();
        var node2 = new PassThroughNode();
        var node3 = new PassThroughNode();

        var inst1 = builder.AddNode(node1);
        var inst2 = builder.AddNode(node2);
        var inst3 = builder.AddNode(node3);

        builder.AddConnection(inst1, node1.Out, inst2, node2.In);
        builder.AddConnection(inst2, node2.Out, inst3, node3.In);

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddConnection(inst3, node3.Out, inst1, node1.In)
        );
    }

    [Fact]
    public void AddConnection_SelfLoop_Throws() {
        var builder = CreateBuilder();
        var node = new PassThroughNode();
        var inst = builder.AddNode(node);

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddConnection(inst, node.Out, inst, node.In)
        );
    }

    [Fact]
    public void Build_ReturnsPipelineWithCorrectTopology() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        var pipeline = builder.Build();

        Assert.Equal(2, pipeline.Topology.Nodes.Count());
        Assert.Single(pipeline.Edges);
    }

    [Fact]
    public void Build_EmptyPipeline_NoNodesNoEdges() {
        var builder = CreateBuilder();
        var pipeline = builder.Build();

        Assert.Empty(pipeline.Topology.Nodes);
        Assert.Empty(pipeline.Edges);
    }

    [Fact]
    public void Build_SingleNode_NoEdges() {
        var builder = CreateBuilder();
        builder.AddNode(new AddNode());

        var pipeline = builder.Build();

        Assert.Single(pipeline.Topology.Nodes);
        Assert.Empty(pipeline.Edges);
    }

    [Fact]
    public void Build_DiamondDependency_CorrectEdgeCount() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode1 = new SubtractNode();
        var subNode2 = new SubtractNode();
        var mulNode = new MultiplyNode();

        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(subNode1);
        var subInst2 = builder.AddNode(subNode2);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst1, subNode1.Number1);
        builder.AddConnection(addInst, addNode.Sum, subInst2, subNode2.Number1);
        builder.AddConnection(subInst1, subNode1.Diff, mulInst, mulNode.Number1);
        builder.AddConnection(subInst2, subNode2.Diff, mulInst, mulNode.Number2);

        var pipeline = builder.Build();

        Assert.Equal(4, pipeline.Topology.Nodes.Count());
        Assert.Equal(4, pipeline.Edges.Length);
    }

    [Fact]
    public void AddConnection_MultipleEdgesPreserveMappings() {
        var builder = CreateBuilder();
        var addNode = new AddNode();
        var subNode = new SubtractNode();
        var mulNode = new MultiplyNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number1);
        builder.AddConnection(subInst, subNode.Diff, mulInst, mulNode.Number1);

        Assert.Equal(addInst.Mappings[addNode.Sum], subInst.Mappings[subNode.Number1]);
        Assert.Equal(subInst.Mappings[subNode.Diff], mulInst.Mappings[mulNode.Number1]);
    }
}
