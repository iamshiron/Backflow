using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineContextTests {
    [Fact]
    public void Write_Read_RoundtripViaNodeInstance() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();
        context.Write(inst, node.Number1, 42);

        Assert.Equal(42, context.Read(inst, node.Number1));
    }

    [Fact]
    public void Write_Read_RoundtripViaGuid() {
        var context = new PipelineContext();
        var id = Guid.NewGuid();

        context.Write(id, "hello");
        Assert.Equal("hello", context.Read(id));
    }

    [Fact]
    public void Write_OverwritesPreviousValue() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();
        context.Write(inst, node.Number1, 10);
        context.Write(inst, node.Number1, 20);

        Assert.Equal(20, context.Read(inst, node.Number1));
    }

    [Fact]
    public void Read_UnwrittenPort_Throws() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();

        Assert.Throws<KeyNotFoundException>(() => context.Read(inst, node.Number1));
    }

    [Fact]
    public void Read_UnwrittenGuid_Throws() {
        var context = new PipelineContext();

        Assert.Throws<KeyNotFoundException>(() => context.Read(Guid.NewGuid()));
    }

    [Fact]
    public void Write_DifferentPortsOnSameNode_Independent() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();
        context.Write(inst, node.Number1, 10);
        context.Write(inst, node.Number2, 20);

        Assert.Equal(10, context.Read(inst, node.Number1));
        Assert.Equal(20, context.Read(inst, node.Number2));
    }

    [Fact]
    public void Write_SameNodeDifferentInstances_Independent() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst1 = builder.AddNode(node);
        var inst2 = builder.AddNode(node);

        var context = new PipelineContext();
        context.Write(inst1, node.Number1, 100);
        context.Write(inst2, node.Number1, 200);

        Assert.Equal(100, context.Read(inst1, node.Number1));
        Assert.Equal(200, context.Read(inst2, node.Number1));
    }

    [Fact]
    public void Write_ConnectionsShareMemory() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        var context = new PipelineContext();
        context.Write(addInst, addNode.Sum, 42);

        Assert.Equal(42, context.Read(subInst, subNode.Number2));
    }

    [Fact]
    public void Write_FanOut_AllDestinationsReadSameValue() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var addNode = new AddNode();
        var sub1 = new SubtractNode();
        var sub2 = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(sub1);
        var subInst2 = builder.AddNode(sub2);

        builder.AddConnection(addInst, addNode.Sum, subInst1, sub1.Number2);
        builder.AddConnection(addInst, addNode.Sum, subInst2, sub2.Number2);

        var context = new PipelineContext();
        context.Write(addInst, addNode.Sum, 55);

        Assert.Equal(55, context.Read(subInst1, sub1.Number2));
        Assert.Equal(55, context.Read(subInst2, sub2.Number2));
    }

    [Fact]
    public void Write_VariousTypes_StoresCorrectly() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();
        context.Write(inst, node.Number1, 42);
        context.Write(inst, node.Number2, "test");

        Assert.Equal(42, context.Read(inst, node.Number1));
        Assert.Equal("test", context.Read(inst, node.Number2));
    }
}

public class NodeContextTests {
    [Fact]
    public void Write_Read_Roundtrip() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var global = new PipelineContext();
        var nodeContext = new NodeContext(global, inst.Mappings);

        nodeContext.Write(node.Number1, 42);
        Assert.Equal(42, nodeContext.Read(node.Number1));
    }

    [Fact]
    public void Write_Read_PropagatesToGlobalContext() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var global = new PipelineContext();
        var nodeContext = new NodeContext(global, inst.Mappings);

        nodeContext.Write(node.Number1, 42);

        Assert.Equal(42, global.Read(inst, node.Number1));
    }
}

public class PortTests {
    [Fact]
    public void Port_Write_Read_Roundtrip() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var global = new PipelineContext();
        var nodeContext = new NodeContext(global, inst.Mappings);

        node.Number1.Write(nodeContext, 42);
        Assert.Equal(42, node.Number1.Read<int>(nodeContext));
    }

    [Fact]
    public void Port_HasUniqueIds() {
        var node = new AddNode();

        var allPortIds = node.Ports.Select(p => p.ID).ToList();
        Assert.Equal(allPortIds.Count, allPortIds.Distinct().Count());
    }

    [Fact]
    public void Port_Name_MatchesConstructor() {
        var node = new AddNode();

        Assert.Equal("Number1", node.Number1.Name);
        Assert.Equal("Number2", node.Number2.Name);
        Assert.Equal("Sum", node.Sum.Name);
    }

    [Fact]
    public void Port_ToString_ContainsName() {
        var node = new AddNode();
        var str = node.Number1.ToString();

        Assert.Contains("Number1", str);
    }
}

public class AbstractNodeTests {
    [Fact]
    public void Ports_ContainsBothInputsAndOutputs() {
        var node = new AddNode();

        var ports = node.Ports.ToList();
        Assert.Equal(3, ports.Count);
        Assert.Contains(node.Number1, ports);
        Assert.Contains(node.Number2, ports);
        Assert.Contains(node.Sum, ports);
    }

    [Fact]
    public void Inputs_And_Outputs_AreSeparate() {
        var node = new AddNode();

        Assert.Equal(2, node.Inputs.Count);
        Assert.Single(node.Outputs);
        Assert.DoesNotContain(node.Sum, node.Inputs);
        Assert.DoesNotContain(node.Number1, node.Outputs);
    }

    [Fact]
    public void Node_NoInputs_OnlyOutputs() {
        var node = new NoInputNode();

        Assert.Empty(node.Inputs);
        Assert.Single(node.Outputs);
    }
}
