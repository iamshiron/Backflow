using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class NodeStateTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SuccessNode : AbstractNode {
        public readonly IOutputPort<int> Out = new OutputPort<int>("out");
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    private class FailingNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(false);
    }

    private class ThrowingNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => throw new InvalidOperationException("boom");
    }

    private class SuccessNodeWithPorts : AbstractNode {
        public readonly IOutputPort<int> Out;
        public SuccessNodeWithPorts() {
            Out = Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, 42);
            return new ValueTask<bool>(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void NodeState_None_IsZero() {
        Assert.Equal((byte) 0, (byte) NodeState.None);
    }

    [Fact]
    public void NodeState_Values_AreDistinctPowersOfTwo() {
        var values = new[] { NodeState.Pending, NodeState.Executing, NodeState.Done, NodeState.Failed, NodeState.Skipped };
        var seen = new HashSet<byte>();
        foreach (var v in values) {
            var b = (byte) v;
            Assert.NotEqual(0, b);
            Assert.True((b & (b - 1)) == 0, $"{v} is not a power of two");
            Assert.True(seen.Add(b), $"{v} duplicates another value");
        }
    }

    [Fact]
    public void NodeState_CanBeCombinedWithFlags() {
        var combined = NodeState.Done | NodeState.Skipped;
        Assert.True(combined.HasFlag(NodeState.Done));
        Assert.True(combined.HasFlag(NodeState.Skipped));
        Assert.False(combined.HasFlag(NodeState.Failed));
    }

    [Fact]
    public void State_DefaultIsPending() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        var inst = builder.AddNode(node);
        Assert.Equal(NodeState.Pending, inst.State);
    }

    [Fact]
    public void Execute_SuccessfulNode_StateIsDone() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        new PipelineExecutor(pipeline).Execute(ArrayPipelineContext.ForPipeline(pipeline));
        Assert.Equal(NodeState.Done, inst.State);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulNode_StateIsDone() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        await new PipelineExecutor(pipeline).ExecuteAsync(ArrayPipelineContext.ForPipeline(pipeline));
        Assert.Equal(NodeState.Done, inst.State);
    }

    [Fact]
    public void Execute_FailingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new FailingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        Assert.Throws<NodeExecutionException>(() => new PipelineExecutor(pipeline).Execute(ArrayPipelineContext.ForPipeline(pipeline)));
        Assert.Equal(NodeState.Failed, inst.State);
    }

    [Fact]
    public async Task ExecuteAsync_FailingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new FailingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        await Assert.ThrowsAsync<NodeExecutionException>(() =>
            new PipelineExecutor(pipeline).ExecuteAsync(ArrayPipelineContext.ForPipeline(pipeline)));
        Assert.Equal(NodeState.Failed, inst.State);
    }

    [Fact]
    public void Execute_ThrowingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new ThrowingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        Assert.Throws<NodeExecutionException>(() => new PipelineExecutor(pipeline).Execute(ArrayPipelineContext.ForPipeline(pipeline)));
        Assert.Equal(NodeState.Failed, inst.State);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new ThrowingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        await Assert.ThrowsAsync<NodeExecutionException>(() =>
            new PipelineExecutor(pipeline).ExecuteAsync(ArrayPipelineContext.ForPipeline(pipeline)));
        Assert.Equal(NodeState.Failed, inst.State);
    }

    [Fact]
    public void Execute_MultipleNodes_AllStatesTransitionCorrectly() {
        var builder = new PipelineBuilder(_registry);
        var instA = builder.AddNode(new SuccessNode());
        var instB = builder.AddNode(new SuccessNode());
        var pipeline = builder.Build();
        new PipelineExecutor(pipeline).Execute(ArrayPipelineContext.ForPipeline(pipeline));
        Assert.Equal(NodeState.Done, instA.State);
        Assert.Equal(NodeState.Done, instB.State);
    }

}
