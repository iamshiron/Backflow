using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Serialization;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineSerializationTests {
    private NodeRegistry CreateRegistry(params AbstractNode[] nodes) {
        var registry = new NodeRegistry();
        foreach (var node in nodes) registry.Register(node);
        return registry;
    }

    [Fact]
    public void ToDto_EmptyPipeline_NoNodesNoEdges() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var pipeline = builder.Build();

        var dto = pipeline.ToDto();

        Assert.Empty(dto.Nodes);
        Assert.Empty(dto.Edges);
    }

    [Fact]
    public void ToDto_SingleNode_OneNodeNoEdges() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();

        var dto = pipeline.ToDto();

        Assert.Single(dto.Nodes);
        Assert.Empty(dto.Edges);
        Assert.Equal(inst.ID, dto.Nodes[0].Id);
        Assert.Equal(node.GetType().FullName, dto.Nodes[0].NodeTypeName);
    }

    [Fact]
    public void ToDto_PreservesPortMappings() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();

        var dto = pipeline.ToDto();

        var mappings = dto.Nodes[0].PortMappings;
        Assert.Equal(3, mappings.Count);
        Assert.Equal(inst.Mappings[node.Number1], mappings[nameof(AddNode.Number1)]);
        Assert.Equal(inst.Mappings[node.Number2], mappings[nameof(AddNode.Number2)]);
        Assert.Equal(inst.Mappings[node.Sum], mappings[nameof(AddNode.Sum)]);
    }

    [Fact]
    public void ToDto_PreservesEdgeInfo() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var addNode = new AddNode();
        var subNode = new SubtractNode();
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        var pipeline = builder.Build();

        var dto = pipeline.ToDto();

        Assert.Single(dto.Edges);
        Assert.Equal(addInst.ID, dto.Edges[0].SourceNodeId);
        Assert.Equal(subInst.ID, dto.Edges[0].DestinationNodeId);
        Assert.Equal(nameof(AddNode.Sum), dto.Edges[0].SourcePortName);
        Assert.Equal(nameof(SubtractNode.Number2), dto.Edges[0].DestinationPortName);
    }

    [Fact]
    public void FromDto_SingleNode_ReconstructsPipeline() {
        var registry = CreateRegistry(new AddNode());
        var builder = new PipelineBuilder(registry);
        var node = registry.Get<AddNode>()!;
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();

        var dto = pipeline.ToDto();
        var restored = dto.FromDto(registry);

        Assert.Single(restored.Topology.Nodes);
        Assert.Empty(restored.Edges);
    }

    [Fact]
    public void FromDto_PreservesNodeIds() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        var pipeline = builder.Build();

        var restored = pipeline.ToDto().FromDto(registry);
        var restoredIds = restored.Topology.Nodes.Select(n => n.ID).ToHashSet();

        Assert.Contains(addInst.ID, restoredIds);
        Assert.Contains(subInst.ID, restoredIds);
    }

    [Fact]
    public void FromDto_PreservesEdgeCount() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        var pipeline = builder.Build();

        var restored = pipeline.ToDto().FromDto(registry);

        Assert.Single(restored.Edges);
    }

    [Fact]
    public void FromDto_DiamondDependency_PreservesAllEdges() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode(), new MultiplyNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var mulNode = registry.Get<MultiplyNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(subNode);
        var subInst2 = builder.AddNode(subNode);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst1, subNode.Number1);
        builder.AddConnection(addInst, addNode.Sum, subInst2, subNode.Number1);
        builder.AddConnection(subInst1, subNode.Diff, mulInst, mulNode.Number1);
        builder.AddConnection(subInst2, subNode.Diff, mulInst, mulNode.Number2);
        var pipeline = builder.Build();

        var restored = pipeline.ToDto().FromDto(registry);

        Assert.Equal(4, restored.Topology.Nodes.Count());
        Assert.Equal(4, restored.Edges.Length);
    }

    [Fact]
    public void FromDto_PreservesPortMappings() {
        var registry = CreateRegistry(new AddNode());
        var builder = new PipelineBuilder(registry);
        var node = registry.Get<AddNode>()!;
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();

        var restored = pipeline.ToDto().FromDto(registry);
        var restoredInst = restored.Topology.Nodes.First();

        Assert.Equal(inst.Mappings[node.Number1], restoredInst.Mappings[node.Number1]);
        Assert.Equal(inst.Mappings[node.Number2], restoredInst.Mappings[node.Number2]);
        Assert.Equal(inst.Mappings[node.Sum], restoredInst.Mappings[node.Sum]);
    }

    [Fact]
    public void FromDto_ConnectionSharesMapping() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        var pipeline = builder.Build();

        var restored = pipeline.ToDto().FromDto(registry);
        var restoredAdd = restored.Topology.Nodes.First(n => n.ID == addInst.ID);
        var restoredSub = restored.Topology.Nodes.First(n => n.ID == subInst.ID);

        Assert.Equal(restoredAdd.Mappings[addNode.Sum], restoredSub.Mappings[subNode.Number2]);
    }

    [Fact]
    public void FromDto_UnknownNodeType_Throws() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        builder.AddNode(node);
        var pipeline = builder.Build();

        var dto = pipeline.ToDto();

        Assert.Throws<InvalidOperationException>(() => dto.FromDto(registry));
    }

    [Fact]
    public void Serialize_Roundtrip_EmptyPipeline() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var pipeline = builder.Build();

        var json = pipeline.Serialize();
        var restored = PipelineSerialization.DeserializePipeline(json, new NodeRegistry());

        Assert.Empty(restored.Topology.Nodes);
        Assert.Empty(restored.Edges);
    }

    [Fact]
    public void Serialize_Roundtrip_LinearPipeline() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        var pipeline = builder.Build();

        var json = pipeline.Serialize();
        var restored = PipelineSerialization.DeserializePipeline(json, registry);

        Assert.Equal(2, restored.Topology.Nodes.Count());
        Assert.Single(restored.Edges);
    }

    [Fact]
    public void Serialize_Roundtrip_ProducesValidJson() {
        var registry = CreateRegistry(new AddNode());
        var builder = new PipelineBuilder(registry);
        var node = registry.Get<AddNode>()!;
        builder.AddNode(node);
        var pipeline = builder.Build();

        var json = pipeline.Serialize();

        Assert.NotEmpty(json);
        Assert.Contains(nameof(AddNode), json);
        Assert.Contains(nameof(AddNode.Number1), json);
        Assert.Contains(nameof(AddNode.Number2), json);
        Assert.Contains(nameof(AddNode.Sum), json);
    }

    [Fact]
    public void Serialize_Deserialize_ExecutionProducesSameResults() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        var pipeline = builder.Build();

        var json = pipeline.Serialize();
        var restored = PipelineSerialization.DeserializePipeline(json, registry);

        var restoredAdd = restored.Topology.Nodes.First(n => n.ID == addInst.ID);
        var restoredSub = restored.Topology.Nodes.First(n => n.ID == subInst.ID);

        var context = new PipelineContext();
        context.Write(restoredAdd, addNode.Number1, 10);
        context.Write(restoredAdd, addNode.Number2, 5);
        context.Write(restoredSub, subNode.Number1, 100);

        var executor = new PipelineExecutor(restored);
        executor.Execute(context);

        Assert.Equal(15, context.Read(restoredAdd, addNode.Sum));
        Assert.Equal(85, context.Read(restoredSub, subNode.Diff));
    }

    [Fact]
    public void Serialize_Deserialize_DiamondExecutionProducesSameResults() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode(), new MultiplyNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var mulNode = registry.Get<MultiplyNode>()!;

        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(subNode);
        var subInst2 = builder.AddNode(subNode);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst1, subNode.Number1);
        builder.AddConnection(addInst, addNode.Sum, subInst2, subNode.Number1);
        builder.AddConnection(subInst1, subNode.Diff, mulInst, mulNode.Number1);
        builder.AddConnection(subInst2, subNode.Diff, mulInst, mulNode.Number2);
        var pipeline = builder.Build();

        var json = pipeline.Serialize();
        var restored = PipelineSerialization.DeserializePipeline(json, registry);

        var rAdd = restored.Topology.Nodes.First(n => n.ID == addInst.ID);
        var rSub1 = restored.Topology.Nodes.First(n => n.ID == subInst1.ID);
        var rSub2 = restored.Topology.Nodes.First(n => n.ID == subInst2.ID);
        var rMul = restored.Topology.Nodes.First(n => n.ID == mulInst.ID);

        var context = new PipelineContext();
        context.Write(rAdd, addNode.Number1, 20);
        context.Write(rAdd, addNode.Number2, 5);
        context.Write(rSub1, subNode.Number2, 3);
        context.Write(rSub2, subNode.Number2, 2);

        var executor = new PipelineExecutor(restored);
        executor.Execute(context);

        Assert.Equal(25, context.Read(rAdd, addNode.Sum));
        Assert.Equal(22, context.Read(rSub1, subNode.Diff));
        Assert.Equal(23, context.Read(rSub2, subNode.Diff));
        Assert.Equal(22 * 23, context.Read(rMul, mulNode.Product));
    }

    [Fact]
    public void DeserializePipeline_InvalidJson_Throws() {
        var registry = new NodeRegistry();
        Assert.Throws<JsonException>(() =>
            PipelineSerialization.DeserializePipeline("not valid json", registry)
        );
    }

    [Fact]
    public void Serialize_Roundtrip_PreservesFanOut() {
        var registry = CreateRegistry(new AddNode(), new SubtractNode(), new MultiplyNode());
        var builder = new PipelineBuilder(registry);
        var addNode = registry.Get<AddNode>()!;
        var subNode = registry.Get<SubtractNode>()!;
        var mulNode = registry.Get<MultiplyNode>()!;
        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        builder.AddConnection(addInst, addNode.Sum, mulInst, mulNode.Number2);
        var pipeline = builder.Build();

        var restored = pipeline.ToDto().FromDto(registry);

        Assert.Equal(3, restored.Topology.Nodes.Count());
        Assert.Equal(2, restored.Edges.Length);

        var rAdd = restored.Topology.Nodes.First(n => n.ID == addInst.ID);
        var rSub = restored.Topology.Nodes.First(n => n.ID == subInst.ID);
        var rMul = restored.Topology.Nodes.First(n => n.ID == mulInst.ID);

        Assert.Equal(rAdd.Mappings[addNode.Sum], rSub.Mappings[subNode.Number2]);
        Assert.Equal(rAdd.Mappings[addNode.Sum], rMul.Mappings[mulNode.Number2]);
    }
}
