using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Node.Behvaior;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class EnableOutBehaviorTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SuccessNodeWithEnableOut : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;
        public readonly EnableOutBehavior EnableOutBehavior;

        public SuccessNodeWithEnableOut() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
            EnableOutBehavior = new EnableOutBehavior();
            AddBehavior(EnableOutBehavior);
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Out.Write(context, In.Read(context) * 2);
            return ValueTask.FromResult(true);
        }
    }

    private class FailingNodeWithEnableOut : AbstractNode {
        public readonly EnableOutBehavior EnableOutBehavior;

        public FailingNodeWithEnableOut() {
            EnableOutBehavior = new EnableOutBehavior();
            AddBehavior(EnableOutBehavior);
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return ValueTask.FromResult(false);
        }
    }

    private class ThrowingNodeWithEnableOut : AbstractNode {
        public readonly EnableOutBehavior EnableOutBehavior;

        public ThrowingNodeWithEnableOut() {
            EnableOutBehavior = new EnableOutBehavior();
            AddBehavior(EnableOutBehavior);
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            throw new InvalidOperationException("Intentional failure");
        }
    }

    private static (ArrayPipelineContext ctx, NodeContext nodeCtx, PipelineBuilder.NodeInstance inst) Setup(AbstractNode node) {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);
        return (ctx, new NodeContext(ctx, inst.Mappings), inst);
    }

    [Fact]
    public void AttachPorts_RegistersEnableOutPort() {
        var node = new SuccessNodeWithEnableOut();

        Assert.Contains(node.Outputs, p => p.Name == "EnableOut");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulNode_WritesTrueToEnableOut() {
        var node = new SuccessNodeWithEnableOut();
        var (pipelineCtx, nodeCtx, inst) = Setup(node);

        pipelineCtx.Write(inst, node.In, 21);

        var state = await node.ExecuteAsync(nodeCtx);

        Assert.Equal(NodeState.Done, state);
        var outPort = node.Ports.First(p => p.Name == "EnableOut");
        Assert.True(pipelineCtx.Read<bool>(inst.Mappings[outPort]));
    }

    [Fact]
    public async Task ExecuteAsync_FailingNode_WritesFalseToEnableOut() {
        var node = new FailingNodeWithEnableOut();
        var (pipelineCtx, nodeCtx, inst) = Setup(node);

        var state = await node.ExecuteAsync(nodeCtx);

        Assert.Equal(NodeState.Failed, state);
        var outPort = node.Ports.First(p => p.Name == "EnableOut");
        Assert.False(pipelineCtx.Read<bool>(inst.Mappings[outPort]));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingNode_DoesNotWriteToEnableOut() {
        var node = new ThrowingNodeWithEnableOut();
        var (_, nodeCtx, _) = Setup(node);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            node.ExecuteAsync(nodeCtx).AsTask());
    }

    [Fact]
    public async Task PreExecuteAsync_AlwaysReturnsShouldContinueTrue() {
        var behavior = new EnableOutBehavior();
        var node = new SuccessNodeWithEnableOut();
        var (_, nodeCtx, _) = Setup(node);

        var (shouldContinue, result) = await behavior.PreExecuteAsync(nodeCtx);

        Assert.True(shouldContinue);
        Assert.True(result);
    }

    [Fact]
    public void EnableOutPort_IsOnOutputPortsList() {
        var node = new SuccessNodeWithEnableOut();

        Assert.Equal(2, node.Outputs.Count);
        Assert.Contains(node.Outputs, p => p.Name == "EnableOut");
        Assert.Contains(node.Outputs, p => p.Name == "out");
    }

    [Fact]
    public void EnableOut_DefaultValue_IsFalse() {
        var node = new SuccessNodeWithEnableOut();
        var (pipelineCtx, _, inst) = Setup(node);

        var outPort = node.Ports.First(p => p.Name == "EnableOut");
        Assert.False(pipelineCtx.HasAny(inst.Mappings[outPort]));
    }
}

public class NodeBehaviorInteractionTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class MultiBehaviorNode : AbstractNode {
        public readonly IOutputPort<int> Out;
        public readonly EnableOutBehavior EnableOut;
        public readonly ChipEnableBehavior ChipEnableBehavior;

        public MultiBehaviorNode() {
            ChipEnableBehavior = new ChipEnableBehavior();
            EnableOut = new EnableOutBehavior();
            AddBehavior(ChipEnableBehavior);
            AddBehavior(EnableOut);

            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Out.Write(context, 42);
            return ValueTask.FromResult(true);
        }
    }

    private static (ArrayPipelineContext ctx, NodeContext nodeCtx, PipelineBuilder.NodeInstance inst) Setup(AbstractNode node) {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var inst = builder.AddNode(node);
        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);
        return (ctx, new NodeContext(ctx, inst.Mappings), inst);
    }

    [Fact]
    public async Task MultipleBehaviors_BothExecute() {
        var node = new MultiBehaviorNode();
        var (pipelineCtx, nodeCtx, inst) = Setup(node);

        var chipPort = node.Ports.First(p => p.Name == "ChipEnable");
        pipelineCtx.Write(inst.Mappings[chipPort], true);

        var state = await node.ExecuteAsync(nodeCtx);

        Assert.Equal(NodeState.Done, state);
        var outPort = node.Ports.First(p => p.Name == "EnableOut");
        Assert.True(pipelineCtx.Read<bool>(inst.Mappings[outPort]));
    }

    [Fact]
    public async Task ChipEnableDisabled_CoreExecutionSkipped_EnableOutReceivesSkippedResult() {
        var node = new MultiBehaviorNode();
        var (pipelineCtx, nodeCtx, inst) = Setup(node);

        var chipPort = node.Ports.First(p => p.Name == "ChipEnable");
        pipelineCtx.Write(inst.Mappings[chipPort], false);

        var state = await node.ExecuteAsync(nodeCtx);

        Assert.Equal(NodeState.Skipped, state);
        var outPort = node.Ports.First(p => p.Name == "EnableOut");
        Assert.False(pipelineCtx.Read<bool>(inst.Mappings[outPort]));
    }
}
