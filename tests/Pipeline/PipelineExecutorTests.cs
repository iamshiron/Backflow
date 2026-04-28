using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineExecutorTests {
    [Fact]
    public void Execute_SingleNode_ExecutesNode() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new NoInputNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();
        var executor = new PipelineExecutor(builder.Build());

        executor.Execute(context);

        Assert.True(node.WasExecuted);
    }

    [Fact]
    public void Execute_SingleNode_WritesOutput() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new NoInputNode();
        var inst = builder.AddNode(node);

        var context = new PipelineContext();
        var executor = new PipelineExecutor(builder.Build());

        executor.Execute(context);

        var result = context.Read(inst, node.Result);
        Assert.Equal(42, result);
    }

    [Fact]
    public void Execute_LinearPipeline_DataFlowsThrough() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        var context = new PipelineContext();
        context.Write(addInst, addNode.Number1, 10);
        context.Write(addInst, addNode.Number2, 5);
        context.Write(subInst, subNode.Number1, 100);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(context);

        Assert.Equal(15, context.Read(addInst, addNode.Sum));
        Assert.Equal(85, context.Read(subInst, subNode.Diff));
    }

    [Fact]
    public void Execute_FanOut_OneOutputFeedsMultipleNodes() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var addNode = new AddNode();
        var subNode = new SubtractNode();
        var mulNode = new MultiplyNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);
        builder.AddConnection(addInst, addNode.Sum, mulInst, mulNode.Number2);

        var context = new PipelineContext();
        context.Write(addInst, addNode.Number1, 10);
        context.Write(addInst, addNode.Number2, 5);
        context.Write(subInst, subNode.Number1, 100);
        context.Write(mulInst, mulNode.Number1, 3);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(context);

        Assert.Equal(15, context.Read(addInst, addNode.Sum));
        Assert.Equal(85, context.Read(subInst, subNode.Diff));
        Assert.Equal(45, context.Read(mulInst, mulNode.Product));
    }

    [Fact]
    public void Execute_DiamondDependency_AllNodesExecuteOnce() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);

        var addNode = new AddNode();
        var sub1 = new SubtractNode();
        var sub2 = new SubtractNode();
        var mulNode = new MultiplyNode();

        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(sub1);
        var subInst2 = builder.AddNode(sub2);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst1, sub1.Number1);
        builder.AddConnection(addInst, addNode.Sum, subInst2, sub2.Number1);
        builder.AddConnection(subInst1, sub1.Diff, mulInst, mulNode.Number1);
        builder.AddConnection(subInst2, sub2.Diff, mulInst, mulNode.Number2);

        var context = new PipelineContext();
        context.Write(addInst, addNode.Number1, 20);
        context.Write(addInst, addNode.Number2, 5);
        context.Write(subInst1, sub1.Number2, 3);
        context.Write(subInst2, sub2.Number2, 2);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(context);

        Assert.Equal(25, context.Read(addInst, addNode.Sum));
        Assert.Equal(22, context.Read(subInst1, sub1.Diff));
        Assert.Equal(23, context.Read(subInst2, sub2.Diff));
        Assert.Equal(22 * 23, context.Read(mulInst, mulNode.Product));
    }

    [Fact]
    public void Layers_SingleNode_SingleLayer() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new NoInputNode();
        builder.AddNode(node);

        var executor = new PipelineExecutor(builder.Build());

        Assert.Single(executor.Layers);
        Assert.Single(executor.Layers[0]);
    }

    [Fact]
    public void Layers_LinearPipeline_MultipleLayers() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var addNode = new AddNode();
        var subNode = new SubtractNode();

        var addInst = builder.AddNode(addNode);
        var subInst = builder.AddNode(subNode);

        builder.AddConnection(addInst, addNode.Sum, subInst, subNode.Number2);

        var executor = new PipelineExecutor(builder.Build());

        Assert.Equal(2, executor.Layers.Length);
        Assert.Single(executor.Layers[0]);
        Assert.Single(executor.Layers[1]);
    }

    [Fact]
    public void Layers_DiamondDependency_ThreeLayers() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var addNode = new AddNode();
        var sub1 = new SubtractNode();
        var sub2 = new SubtractNode();
        var mulNode = new MultiplyNode();

        var addInst = builder.AddNode(addNode);
        var subInst1 = builder.AddNode(sub1);
        var subInst2 = builder.AddNode(sub2);
        var mulInst = builder.AddNode(mulNode);

        builder.AddConnection(addInst, addNode.Sum, subInst1, sub1.Number1);
        builder.AddConnection(addInst, addNode.Sum, subInst2, sub2.Number1);
        builder.AddConnection(subInst1, sub1.Diff, mulInst, mulNode.Number1);
        builder.AddConnection(subInst2, sub2.Diff, mulInst, mulNode.Number2);

        var executor = new PipelineExecutor(builder.Build());

        Assert.Equal(3, executor.Layers.Length);
        Assert.Single(executor.Layers[0]);
        Assert.Equal(2, executor.Layers[1].Length);
        Assert.Single(executor.Layers[2]);
    }

    [Fact]
    public void Execute_EmptyPipeline_DoesNotThrow() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var executor = new PipelineExecutor(builder.Build());

        var exception = Record.Exception(() => executor.Execute(new PipelineContext()));
        Assert.Null(exception);
    }

    [Fact]
    public void Execute_PassThroughChain_DataFlowsThroughMultipleStages() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node1 = new PassThroughNode();
        var node2 = new PassThroughNode();
        var node3 = new PassThroughNode();

        var inst1 = builder.AddNode(node1);
        var inst2 = builder.AddNode(node2);
        var inst3 = builder.AddNode(node3);

        builder.AddConnection(inst1, node1.Out, inst2, node2.In);
        builder.AddConnection(inst2, node2.Out, inst3, node3.In);

        var context = new PipelineContext();
        context.Write(inst1, node1.In, 99);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(context);

        Assert.Equal(99, context.Read(inst3, node3.Out));
    }

    [Fact]
    public void Execute_IsolatedNodes_AllExecute() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node1 = new NoInputNode();
        var node2 = new NoInputNode();

        builder.AddNode(node1);
        builder.AddNode(node2);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(new PipelineContext());

        Assert.True(node1.WasExecuted);
        Assert.True(node2.WasExecuted);
    }

    [Fact]
    public void Execute_SameNodeInstanceAddedTwice_BothInstancesExecute() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new CollectorNode();

        var inst1 = builder.AddNode(node);
        var inst2 = builder.AddNode(node);

        var context = new PipelineContext();
        context.Write(inst1, node.Value, 10);
        context.Write(inst2, node.Value, 20);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(context);

        Assert.Equal(2, node.ExecutionCount);
    }

    [Fact]
    public void Execute_SamplePipeline_ProducesCorrectResults() {
        var registry = new NodeRegistry();
        var addNode = registry.Register<AddNode>();
        var printNode = registry.Register<PrintNode>();
        var subtractNode = registry.Register<SubtractNode>();

        var builder = new PipelineBuilder(registry);
        var addInst = builder.AddNode(addNode);
        var subtractInst = builder.AddNode(subtractNode);
        var printInst = builder.AddNode(printNode);
        var printInst2 = builder.AddNode(printNode);

        builder.AddConnection(addInst, addNode.Sum, printInst, printNode.Message);
        builder.AddConnection(addInst, addNode.Sum, subtractInst, subtractNode.Number2);
        builder.AddConnection(subtractInst, subtractNode.Diff, printInst2, printNode.Message);

        IPipelineContext context = new PipelineContext();
        context.Write(addInst, addNode.Number1, 19);
        context.Write(addInst, addNode.Number2, 95);
        context.Write(subtractInst, subtractNode.Number1, 100);

        var executor = new PipelineExecutor(builder.Build());
        executor.Execute(context);

        Assert.Equal(19 + 95, context.Read(addInst, addNode.Sum));
        Assert.Equal(100 - (19 + 95), context.Read(subtractInst, subtractNode.Diff));
    }

    [Fact]
    public void Execute_NodeReturnsFalse_ThrowsNodeExecutionException() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new FailingNode();
        var inst = builder.AddNode(node);

        var executor = new PipelineExecutor(builder.Build());

        var ex = Assert.Throws<NodeExecutionException>(() => executor.Execute(new PipelineContext()));
        Assert.Equal(inst, ex.NodeInstance);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Execute_NodeThrows_ThrowsNodeExecutionExceptionWithInner() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new FailingNode { ShouldThrow = true };
        var inst = builder.AddNode(node);

        var executor = new PipelineExecutor(builder.Build());

        var ex = Assert.Throws<NodeExecutionException>(() => executor.Execute(new PipelineContext()));
        Assert.Equal(inst, ex.NodeInstance);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Execute_NodeExecutionException_ContainsNodeInfo() {
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = new FailingNode();
        builder.AddNode(node);

        var executor = new PipelineExecutor(builder.Build());

        var ex = Assert.Throws<NodeExecutionException>(() => executor.Execute(new PipelineContext()));
        Assert.Contains(nameof(FailingNode), ex.Message);
    }
}
