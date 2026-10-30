using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Exceptions;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;
using Xunit;

namespace Shiron.Backflow.Tests;

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
        Assert.Equal(NodeState.Pending, new PipelineExecutionState().GetNodeState(inst));
    }

    [Fact]
    public void Execute_SuccessfulNode_StateIsDone() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        new PipelineExecutor(pipeline).Execute(context);
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(inst));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulNode_StateIsDone() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        await new PipelineExecutor(pipeline).ExecuteAsync(context);
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(inst));
    }

    [Fact]
    public void Execute_FailingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new FailingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        Assert.Throws<NodeExecutionException>(() => new PipelineExecutor(pipeline).Execute(context));
        Assert.Equal(NodeState.Failed, context.Execution.GetNodeState(inst));
    }

    [Fact]
    public async Task ExecuteAsync_FailingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new FailingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        await Assert.ThrowsAsync<NodeExecutionException>(() => new PipelineExecutor(pipeline).ExecuteAsync(context));
        Assert.Equal(NodeState.Failed, context.Execution.GetNodeState(inst));
    }

    [Fact]
    public void Execute_ThrowingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new ThrowingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        Assert.Throws<NodeExecutionException>(() => new PipelineExecutor(pipeline).Execute(context));
        Assert.Equal(NodeState.Failed, context.Execution.GetNodeState(inst));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new ThrowingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        await Assert.ThrowsAsync<NodeExecutionException>(() => new PipelineExecutor(pipeline).ExecuteAsync(context));
        Assert.Equal(NodeState.Failed, context.Execution.GetNodeState(inst));
    }

    [Fact]
    public void Execute_MultipleNodes_AllStatesTransitionCorrectly() {
        var builder = new PipelineBuilder(_registry);
        var instA = builder.AddNode(new SuccessNode());
        var instB = builder.AddNode(new SuccessNode());
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        new PipelineExecutor(pipeline).Execute(context);
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(instA));
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(instB));
    }

}
