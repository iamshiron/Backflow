using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Node.Behvaior;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class SkipPropagationTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SourceWithChipEnable : AbstractNode {
        public readonly IOutputPort<int> Out;
        public readonly ChipEnableBehavior ChipEnableBehavior = new();

        public SourceWithChipEnable() {
            Out = Output(new OutputPort<int>("out"));
            AddBehavior(ChipEnableBehavior);
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, 42);
            return new ValueTask<bool>(true);
        }
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;

        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, 42);
            return new ValueTask<bool>(true);
        }
    }

    private class RelayRequired : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;

        public RelayRequired() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    private class RelayOptional : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;

        public RelayOptional() {
            var port = new InputPort<int>("in", 0, new PassValidator<int>());
            port.IsRequired = false;
            In = Input(port);
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => new(true);
    }

    private class DualInputNode : AbstractNode {
        public readonly IInputPort<int> RequiredIn;
        public readonly IInputPort<int> OptionalIn;
        public readonly IOutputPort<int> Out;
        public bool DidExecute;

        public DualInputNode() {
            RequiredIn = Input(new InputPort<int>("required", 0, new PassValidator<int>()));
            var optPort = new InputPort<int>("optional", 0, new PassValidator<int>());
            optPort.IsRequired = false;
            OptionalIn = Input(optPort);
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            DidExecute = true;
            return new ValueTask<bool>(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void Execute_ChipDisabled_SetsSkipped() {
        var builder = new PipelineBuilder(_registry);
        var node = new SourceWithChipEnable();
        var inst = builder.AddNode(node);

        var ctx = new PipelineContext();
        ctx.Write(inst, node.ChipEnableBehavior.ChipEnable, false);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Skipped, inst.State);
    }

    [Fact]
    public async Task ExecuteAsync_ChipDisabled_SetsSkipped() {
        var builder = new PipelineBuilder(_registry);
        var node = new SourceWithChipEnable();
        var inst = builder.AddNode(node);

        var ctx = new PipelineContext();
        ctx.Write(inst, node.ChipEnableBehavior.ChipEnable, false);

        await new PipelineExecutor(builder.Build()).ExecuteAsync(ctx);
        Assert.Equal(NodeState.Skipped, inst.State);
    }

    [Fact]
    public void Execute_ChipDisabled_RequiredDownstream_SkipsDownstream() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceWithChipEnable();
        var dest = new RelayRequired();

        var srcInst = builder.AddNode(source);
        var destInst = builder.AddNode(dest);
        builder.AddConnection(srcInst, source.Out, destInst, dest.In);

        var ctx = new PipelineContext();
        ctx.Write(srcInst, source.ChipEnableBehavior.ChipEnable, false);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Skipped, srcInst.State);
        Assert.Equal(NodeState.Skipped, destInst.State);
    }

    [Fact]
    public async Task ExecuteAsync_ChipDisabled_RequiredDownstream_SkipsDownstream() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceWithChipEnable();
        var dest = new RelayRequired();

        var srcInst = builder.AddNode(source);
        var destInst = builder.AddNode(dest);
        builder.AddConnection(srcInst, source.Out, destInst, dest.In);

        var ctx = new PipelineContext();
        ctx.Write(srcInst, source.ChipEnableBehavior.ChipEnable, false);

        await new PipelineExecutor(builder.Build()).ExecuteAsync(ctx);
        Assert.Equal(NodeState.Skipped, srcInst.State);
        Assert.Equal(NodeState.Skipped, destInst.State);
    }

    [Fact]
    public void Execute_ChipDisabled_OptionalDownstream_ExecutesDownstream() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceWithChipEnable();
        var dest = new RelayOptional();

        var srcInst = builder.AddNode(source);
        var destInst = builder.AddNode(dest);
        builder.AddConnection(srcInst, source.Out, destInst, dest.In);

        var ctx = new PipelineContext();
        ctx.Write(srcInst, source.ChipEnableBehavior.ChipEnable, false);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Skipped, srcInst.State);
        Assert.Equal(NodeState.Done, destInst.State);
    }

    [Fact]
    public async Task ExecuteAsync_ChipDisabled_OptionalDownstream_ExecutesDownstream() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceWithChipEnable();
        var dest = new RelayOptional();

        var srcInst = builder.AddNode(source);
        var destInst = builder.AddNode(dest);
        builder.AddConnection(srcInst, source.Out, destInst, dest.In);

        var ctx = new PipelineContext();
        ctx.Write(srcInst, source.ChipEnableBehavior.ChipEnable, false);

        await new PipelineExecutor(builder.Build()).ExecuteAsync(ctx);
        Assert.Equal(NodeState.Skipped, srcInst.State);
        Assert.Equal(NodeState.Done, destInst.State);
    }

    [Fact]
    public void Execute_TransitiveSkip_RequiredChain_AllSkip() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceWithChipEnable();
        var relay = new RelayRequired();
        var sink = new RelayRequired();

        var srcInst = builder.AddNode(source);
        var relayInst = builder.AddNode(relay);
        var sinkInst = builder.AddNode(sink);

        builder.AddConnection(srcInst, source.Out, relayInst, relay.In);
        builder.AddConnection(relayInst, relay.Out, sinkInst, sink.In);

        var ctx = new PipelineContext();
        ctx.Write(srcInst, source.ChipEnableBehavior.ChipEnable, false);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Skipped, srcInst.State);
        Assert.Equal(NodeState.Skipped, relayInst.State);
        Assert.Equal(NodeState.Skipped, sinkInst.State);
    }

    [Fact]
    public void Execute_MixedInputs_SkippedRequiredPort_SkipsNode() {
        var builder = new PipelineBuilder(_registry);

        var skippedSource = new SourceWithChipEnable();
        var normalSource = new SourceNode();
        var dest = new DualInputNode();

        var skipInst = builder.AddNode(skippedSource);
        var normInst = builder.AddNode(normalSource);
        var destInst = builder.AddNode(dest);

        builder.AddConnection(skipInst, skippedSource.Out, destInst, dest.RequiredIn);
        builder.AddConnection(normInst, normalSource.Out, destInst, dest.OptionalIn);

        var ctx = new PipelineContext();
        ctx.Write(skipInst, skippedSource.ChipEnableBehavior.ChipEnable, false);
        ctx.Write(normInst, normalSource.Out, 99);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Skipped, skipInst.State);
        Assert.Equal(NodeState.Done, normInst.State);
        Assert.Equal(NodeState.Skipped, destInst.State);
        Assert.False(dest.DidExecute);
    }

    [Fact]
    public void Execute_MixedInputs_SkippedOptionalPort_ExecutesNode() {
        var builder = new PipelineBuilder(_registry);

        var skippedSource = new SourceWithChipEnable();
        var normalSource = new SourceNode();
        var dest = new DualInputNode();

        var skipInst = builder.AddNode(skippedSource);
        var normInst = builder.AddNode(normalSource);
        var destInst = builder.AddNode(dest);

        builder.AddConnection(skipInst, skippedSource.Out, destInst, dest.OptionalIn);
        builder.AddConnection(normInst, normalSource.Out, destInst, dest.RequiredIn);

        var ctx = new PipelineContext();
        ctx.Write(skipInst, skippedSource.ChipEnableBehavior.ChipEnable, false);
        ctx.Write(normInst, normalSource.Out, 99);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Skipped, skipInst.State);
        Assert.Equal(NodeState.Done, normInst.State);
        Assert.Equal(NodeState.Done, destInst.State);
        Assert.True(dest.DidExecute);
    }

    [Fact]
    public void Execute_ChipEnabled_NormalExecution() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceWithChipEnable();
        var dest = new RelayRequired();

        var srcInst = builder.AddNode(source);
        var destInst = builder.AddNode(dest);
        builder.AddConnection(srcInst, source.Out, destInst, dest.In);

        var ctx = new PipelineContext();
        ctx.Write(srcInst, source.ChipEnableBehavior.ChipEnable, true);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Done, srcInst.State);
        Assert.Equal(NodeState.Done, destInst.State);
    }

    [Fact]
    public void Execute_NoIncomingEdges_NeverSkippedByPropagation() {
        var builder = new PipelineBuilder(_registry);
        var inst = builder.AddNode(new SourceNode());
        new PipelineExecutor(builder.Build()).Execute(new PipelineContext());
        Assert.Equal(NodeState.Done, inst.State);
    }

    [Fact]
    public void Execute_SameNodeReused_ChipDisabledInstanceDoesNotAffectOtherInstance() {
        var builder = new PipelineBuilder(_registry);
        var sharedNode = new SourceWithChipEnable();
        var destA = new RelayRequired();
        var destB = new RelayRequired();

        var instA = builder.AddNode(sharedNode);
        var instB = builder.AddNode(sharedNode);
        var destAInst = builder.AddNode(destA);
        var destBInst = builder.AddNode(destB);

        builder.AddConnection(instA, sharedNode.Out, destAInst, destA.In);
        builder.AddConnection(instB, sharedNode.Out, destBInst, destB.In);

        var ctx = new PipelineContext();
        ctx.Write(instA, sharedNode.ChipEnableBehavior.ChipEnable, true);
        ctx.Write(instB, sharedNode.ChipEnableBehavior.ChipEnable, false);

        new PipelineExecutor(builder.Build()).Execute(ctx);
        Assert.Equal(NodeState.Done, destAInst.State);
        Assert.Equal(NodeState.Skipped, destBInst.State);
    }

    [Fact]
    public async Task ExecuteAsync_SameNodeReused_ChipDisabledInstanceDoesNotAffectOtherInstance() {
        var builder = new PipelineBuilder(_registry);
        var sharedNode = new SourceWithChipEnable();
        var destA = new RelayRequired();
        var destB = new RelayRequired();

        var instA = builder.AddNode(sharedNode);
        var instB = builder.AddNode(sharedNode);
        var destAInst = builder.AddNode(destA);
        var destBInst = builder.AddNode(destB);

        builder.AddConnection(instA, sharedNode.Out, destAInst, destA.In);
        builder.AddConnection(instB, sharedNode.Out, destBInst, destB.In);

        var ctx = new PipelineContext();
        ctx.Write(instA, sharedNode.ChipEnableBehavior.ChipEnable, true);
        ctx.Write(instB, sharedNode.ChipEnableBehavior.ChipEnable, false);

        await new PipelineExecutor(builder.Build()).ExecuteAsync(ctx);
        Assert.Equal(NodeState.Done, destAInst.State);
        Assert.Equal(NodeState.Skipped, destBInst.State);
    }
}
