using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;
using Xunit;

namespace Shiron.Backflow.Tests;

public class ExecutionCancellationTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class RecordingNode : AbstractNode {
        public bool DidExecute;
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            DidExecute = true;
            return ValueTask.FromResult(true);
        }
    }

    private class TokenCaptureNode : AbstractNode {
        public CancellationToken ObservedToken;
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            ObservedToken = context.CancellationToken;
            return ValueTask.FromResult(true);
        }
    }

    private class CancelingSourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;
        private readonly CancellationTokenSource _cts;

        public CancelingSourceNode(CancellationTokenSource cts) {
            _cts = cts;
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, 1);
            _cts.Cancel();
            return ValueTask.FromResult(true);
        }
    }

    private class RecordingRelayNode : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;
        public bool DidExecute;

        public RecordingRelayNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            DidExecute = true;
            return ValueTask.FromResult(true);
        }
    }

    private class RespectsCancellationNode(CancellationTokenSource cts) : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            cts.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public async Task ExecuteAsync_DefaultToken_ExecutesNormally() {
        var builder = new PipelineBuilder(_registry);
        var node = new RecordingNode();
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        await new PipelineExecutor(pipeline).ExecuteAsync(ctx);

        Assert.True(node.DidExecute);
        Assert.Equal(NodeState.Done, inst.State);
    }

    [Fact]
    public void Execute_PreCanceledToken_ThrowsAndRunsNoNodes() {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var builder = new PipelineBuilder(_registry);
        var node = new RecordingNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        Assert.Throws<OperationCanceledException>(() =>
            new PipelineExecutor(pipeline).Execute(ctx, cts.Token));

        Assert.False(node.DidExecute);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledToken_ThrowsAndRunsNoNodes() {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var builder = new PipelineBuilder(_registry);
        var node = new RecordingNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new PipelineExecutor(pipeline).ExecuteAsync(ctx, cts.Token));

        Assert.False(node.DidExecute);
    }

    [Fact]
    public void Execute_CancelBetweenLayers_DownstreamNotExecuted() {
        using var cts = new CancellationTokenSource();
        var builder = new PipelineBuilder(_registry);
        var source = new CancelingSourceNode(cts);
        var relay = new RecordingRelayNode();

        var srcInst = builder.AddNode(source);
        var relayInst = builder.AddNode(relay);
        builder.AddConnection(srcInst, source.Out, relayInst, relay.In);

        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        Assert.Throws<OperationCanceledException>(() =>
            new PipelineExecutor(pipeline).Execute(ctx, cts.Token));

        Assert.False(relay.DidExecute);
    }

    [Fact]
    public async Task ExecuteAsync_CancelBetweenLayers_DownstreamNotExecuted() {
        using var cts = new CancellationTokenSource();
        var builder = new PipelineBuilder(_registry);
        var source = new CancelingSourceNode(cts);
        var relay = new RecordingRelayNode();

        var srcInst = builder.AddNode(source);
        var relayInst = builder.AddNode(relay);
        builder.AddConnection(srcInst, source.Out, relayInst, relay.In);

        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new PipelineExecutor(pipeline).ExecuteAsync(ctx, cts.Token));

        Assert.False(relay.DidExecute);
    }

    [Fact]
    public void Execute_NodeObservesExecutorCancellationToken() {
        using var cts = new CancellationTokenSource();
        var builder = new PipelineBuilder(_registry);
        var node = new TokenCaptureNode();
        builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        new PipelineExecutor(pipeline).Execute(ctx, cts.Token);

        Assert.Equal(cts.Token, node.ObservedToken);
    }

    [Fact]
    public void Execute_NodeRespectsToken_PropagatesOperationCanceledException() {
        using var cts = new CancellationTokenSource();
        var builder = new PipelineBuilder(_registry);
        var node = new RespectsCancellationNode(cts);
        builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        Assert.Throws<OperationCanceledException>(() =>
            new PipelineExecutor(pipeline).Execute(ctx, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_NodeRespectsToken_PropagatesOperationCanceledException() {
        using var cts = new CancellationTokenSource();
        var builder = new PipelineBuilder(_registry);
        var node = new RespectsCancellationNode(cts);
        builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new PipelineExecutor(pipeline).ExecuteAsync(ctx, cts.Token));
    }
}
