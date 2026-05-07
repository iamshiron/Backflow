using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
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

    private class CacheHitStub : INodeCache {
        private readonly CacheEntry? _entry;
        public CacheHitStub(CacheEntry? entry) => _entry = entry;
        public ValueTask<CacheEntry?> Get(CacheKey key, CancellationToken ct = default) => new(_entry);
        public ValueTask Set(CacheKey key, CacheEntry entry, CancellationToken ct = default) => default;
        public ValueTask DisposeAsync() => default;
        public void Dispose() { }
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
        var node = new SuccessNode();
        Assert.Equal(NodeState.Pending, node.State);
    }

    [Fact]
    public void Execute_SuccessfulNode_StateIsDone() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        executor.Execute(new PipelineContext());

        Assert.Equal(NodeState.Done, node.State);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulNode_StateIsDone() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await executor.ExecuteAsync(new PipelineContext());

        Assert.Equal(NodeState.Done, node.State);
    }

    [Fact]
    public void Execute_FailingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new FailingNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        Assert.Throws<NodeExecutionException>(() => executor.Execute(new PipelineContext()));

        Assert.Equal(NodeState.Failed, node.State);
    }

    [Fact]
    public async Task ExecuteAsync_FailingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new FailingNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await Assert.ThrowsAsync<NodeExecutionException>(() =>
            executor.ExecuteAsync(new PipelineContext()));

        Assert.Equal(NodeState.Failed, node.State);
    }

    [Fact]
    public void Execute_ThrowingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new ThrowingNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        Assert.Throws<NodeExecutionException>(() => executor.Execute(new PipelineContext()));

        Assert.Equal(NodeState.Failed, node.State);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingNode_StateIsFailed() {
        var builder = new PipelineBuilder(_registry);
        var node = new ThrowingNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await Assert.ThrowsAsync<NodeExecutionException>(() =>
            executor.ExecuteAsync(new PipelineContext()));

        Assert.Equal(NodeState.Failed, node.State);
    }

    [Fact]
    public void Execute_CacheHitNode_StateIsSkipped() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNodeWithPorts();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        var cachedEntry = new CacheEntry();
        cachedEntry.AddOutput("out", typeof(int), 99);

        var cache = new CacheHitStub(cachedEntry);
        executor.Execute(new PipelineContext(), cache);

        Assert.Equal(NodeState.Skipped, node.State);
    }

    [Fact]
    public async Task ExecuteAsync_CacheHitNode_StateIsSkipped() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNodeWithPorts();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        var cachedEntry = new CacheEntry();
        cachedEntry.AddOutput("out", typeof(int), 99);

        var cache = new CacheHitStub(cachedEntry);
        await executor.ExecuteAsync(new PipelineContext(), cache);

        Assert.Equal(NodeState.Skipped, node.State);
    }

    [Fact]
    public void Execute_MultipleNodes_AllStatesTransitionCorrectly() {
        var builder = new PipelineBuilder(_registry);
        var nodeA = new SuccessNode();
        var nodeB = new SuccessNode();
        builder.AddNode(nodeA);
        builder.AddNode(nodeB);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        executor.Execute(new PipelineContext());

        Assert.Equal(NodeState.Done, nodeA.State);
        Assert.Equal(NodeState.Done, nodeB.State);
    }

    [Fact]
    public void Execute_ReExecuteWithCacheHit_SecondRunNodeIsSkipped() {
        var builder = new PipelineBuilder(_registry);
        var node = new SuccessNodeWithPorts();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        executor.Execute(new PipelineContext());
        Assert.Equal(NodeState.Done, node.State);

        var cachedEntry = new CacheEntry();
        cachedEntry.AddOutput("out", typeof(int), 42);
        var cache = new CacheHitStub(cachedEntry);

        executor.Execute(new PipelineContext(), cache);
        Assert.Equal(NodeState.Skipped, node.State);
    }
}
