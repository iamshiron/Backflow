using Shiron.Backflow.Context;
using Shiron.Backflow.Caching;
using Shiron.Backflow.Casting;
using Shiron.Backflow.Exceptions;
using Shiron.Backflow.Node;
using Shiron.Backflow.Node.Behvaior;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Validator;
using Shiron.Backflow.Registry;
using Xunit;

namespace Shiron.Backflow.Tests;

public sealed class ReusableExecutionTests {
    private sealed record RunOptions(int Offset, Task? Gate = null, bool Fail = false);
    private sealed record IdentityOptions(int Offset) : ICacheIdentityProvider {
        public object? CacheIdentity => Offset.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class OptionNode : AbstractNode {
        public IInputPort<int> In { get; }
        public IOutputPort<int> Out { get; }
        public ChipEnableBehavior Enable { get; } = new();

        public OptionNode() {
            In = Input(new InputPort<int>("in", 0, new PassAllPortValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
            AddBehavior(Enable);
        }

        protected override async ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var options = context.GetOptions<RunOptions>();
            var input = context.Read<int>(In);
            if (options?.Gate is { } gate) await gate.WaitAsync(context.CancellationToken);
            if (options?.Fail == true) throw new InvalidOperationException("isolated failure");
            context.Write(Out, input + (options?.Offset ?? 0));
            return true;
        }
    }

    private sealed class CopyNode : AbstractNode {
        public IInputPort<int> In { get; }
        public IOutputPort<int> Out { get; }

        public CopyNode() {
            In = Input(new InputPort<int>("in", 0, new PassAllPortValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, context.Read<int>(In));
            return new ValueTask<bool>(true);
        }
    }

    private sealed class JoinNode : AbstractNode {
        public IInputPort<bool> Fail { get; } = new InputPort<bool>("fail", false, new PassAllPortValidator<bool>());

        public JoinNode() => Input(Fail);

        protected override async ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            if (context.Read<bool>(Fail)) return false;
            await context.GetOptions<RunOptions>()!.Gate!;
            return true;
        }
    }

    private sealed class ArrayNode : AbstractNode {
        public IArrayInputPort<int> Values { get; }
        public IOutputPort<int[]> Out { get; }

        public ArrayNode() {
            Values = Input(new ArrayInputPort<int>("values", 0, new PassAllPortValidator<int>(), new PassAllPortValidator<int[]>(), 0, null));
            Out = Output(new OutputPort<int[]>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, context.Read<int[]>(Values));
            return new ValueTask<bool>(true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FailedSibling_JoinsOutstandingOperationsBeforeReleasingContext() {
        var definition = new JoinNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var failed = builder.AddNode(definition);
        var waiting = builder.AddNode(definition);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(0, release.Task));
        context.Write(failed, definition.Fail, true);
        var execution = executor.ExecuteAsync(context);
        Assert.Equal(NodeState.Failed, context.Execution.GetNodeState(failed));
        Assert.Equal(NodeState.Executing, context.Execution.GetNodeState(waiting));
        Assert.False(execution.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(context));
        release.SetResult();
        await Assert.ThrowsAsync<NodeExecutionException>(() => execution);
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(waiting));
    }

    [Fact]
    public async Task ExecuteAsync_IndexedInputsAndDirectSlots_IsolatesArraysAndSuppliedMasks() {
        var source = new OptionNode();
        var target = new ArrayNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var sourceInstance = builder.AddNode(source);
        var targetInstance = builder.AddNode(target, new Dictionary<string, int> { ["values"] = 2 });
        builder.AddConnection(sourceInstance, source.Out, targetInstance, target.Values, 0);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(10, release.Task));
        var second = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(20, release.Task));
        first.WriteAt(targetInstance, target.Values, 1, 11);
        second.WriteAt(targetInstance, target.Values, 1, 22);
        var firstExecution = executor.ExecuteAsync(first);
        var secondExecution = executor.ExecuteAsync(second);
        release.SetResult();
        await Task.WhenAll(firstExecution, secondExecution);
        var firstArray = Assert.IsType<int[]>(first.Read<int[]>(targetInstance, target.Out));
        var secondArray = Assert.IsType<int[]>(second.Read<int[]>(targetInstance, target.Out));
        Assert.Equal([10, 11], firstArray);
        Assert.Equal([20, 22], secondArray);
        Assert.NotSame(firstArray, secondArray);
        Assert.NotSame(first.GetSuppliedMask(targetInstance.Mappings[target.Values]), second.GetSuppliedMask(targetInstance.Mappings[target.Values]));
    }

    [Fact]
    public async Task ExecuteAsync_SharedDefinitionsAndPipeline_IsolatesPortsOptionsAndPropagation() {
        var registry = new NodeRegistry();
        var definition = registry.Register<OptionNode>();
        var builder = new PipelineBuilder(registry);
        var source = builder.AddNode(definition);
        var sink = builder.AddNode(definition);
        builder.AddConnection(source, definition.Out, sink, definition.In);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(10, release.Task));
        var skipped = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(100));
        running.Write(source, definition.In, 7);
        skipped.Write(source, definition.Enable.ChipEnable, false);

        var pending = executor.ExecuteAsync(running);
        Assert.Equal(NodeState.Executing, running.Execution.GetNodeState(source));
        await executor.ExecuteAsync(skipped);
        Assert.Equal(NodeState.Skipped, skipped.Execution.GetNodeState(source));
        Assert.Equal(NodeState.Skipped, skipped.Execution.GetNodeState(sink));
        Assert.Equal(NodeState.Executing, running.Execution.GetNodeState(source));
        release.SetResult();
        await pending;

        Assert.Equal(27, running.Read<int>(sink, definition.Out));
        Assert.False(skipped.HasAny(sink.Mappings[definition.Out]));
        Assert.Equal(NodeState.Done, running.Execution.GetNodeState(sink));
        Assert.Equal(NodeState.Skipped, skipped.Execution.GetNodeState(sink));
    }

    [Fact]
    public void Execute_ReusedContext_ClearsProducedValuesAndResetsPropagation() {
        var definition = new OptionNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var source = builder.AddNode(definition);
        var sink = builder.AddNode(definition);
        builder.AddConnection(source, definition.Out, sink, definition.In);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var context = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(2));
        context.Write(source, definition.In, 5);
        executor.Execute(context);
        Assert.Equal(9, context.Read<int>(sink, definition.Out));

        context.Write(source, definition.Enable.ChipEnable, false);
        var skippedStats = executor.Execute(context);
        Assert.Equal(2, skippedStats.SkippedNodes);
        Assert.Equal(0, skippedStats.ExecutedNodes);
        Assert.Equal(NodeState.Skipped, context.Execution.GetNodeState(sink));
        Assert.False(context.HasAny(sink.Mappings[definition.Out]));

        context.Write(source, definition.Enable.ChipEnable, true);
        executor.Execute(context);
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(sink));
        Assert.Equal(9, context.Read<int>(sink, definition.Out));
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentCancellation_DoesNotCancelAnotherContext() {
        var definition = new OptionNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = builder.AddNode(definition);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(1, release.Task));
        var successful = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(2, release.Task));
        using var cts = new CancellationTokenSource();
        var first = executor.ExecuteAsync(canceled, cts.Token);
        var second = executor.ExecuteAsync(successful);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(NodeState.Canceled, canceled.Execution.GetNodeState(node));
        Assert.False(second.IsCompleted);
        release.SetResult();
        await second;
        Assert.Equal(2, successful.Read<int>(node, definition.Out));
        Assert.Equal(NodeState.Done, successful.Execution.GetNodeState(node));
        await executor.ExecuteAsync(canceled);
        Assert.Equal(NodeState.Done, canceled.Execution.GetNodeState(node));
    }

    [Fact]
    public async Task ExecuteAsync_SameContextOverlaps_RejectsWithoutDisturbingActiveExecution() {
        var definition = new OptionNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = builder.AddNode(definition);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(3, release.Task));
        var pending = executor.ExecuteAsync(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(context));
        Assert.Equal(NodeState.Executing, context.Execution.GetNodeState(node));
        release.SetResult();
        await pending;
        Assert.Equal(3, context.Read<int>(node, definition.Out));
    }

    [Fact]
    public async Task ExecuteAsync_OneContextThrows_OtherContextRemainsSuccessful() {
        var definition = new OptionNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = builder.AddNode(definition);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(1, release.Task, true));
        var successful = ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(2, release.Task));
        var first = executor.ExecuteAsync(failed);
        var second = executor.ExecuteAsync(successful);
        release.SetResult();
        var exception = await Assert.ThrowsAsync<NodeExecutionException>(() => first);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        await second;
        Assert.Equal(NodeState.Failed, failed.Execution.GetNodeState(node));
        Assert.Equal(NodeState.Done, successful.Execution.GetNodeState(node));
        Assert.Equal(2, successful.Read<int>(node, definition.Out));
    }

    [Fact]
    public async Task ExecuteAsync_ManyConcurrentRuns_IsolatesEveryResult() {
        var definition = new OptionNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var node = builder.AddNode(definition);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var contexts = Enumerable.Range(0, 128).Select(index =>
            ArrayPipelineContext.ForPipeline(pipeline, new RunOptions(index))).ToArray();
        await Task.WhenAll(contexts.Select((context, index) => Task.Run(async () => {
            for (var iteration = 0; iteration < 10; iteration++) {
                context.Write(node, definition.In, iteration);
                await executor.ExecuteValueAsync(context);
                Assert.Equal(index + iteration, context.Read<int>(node, definition.Out));
            }
        })));
    }

    [Fact]
    public void Execute_CompiledGraph_IgnoresLaterBuilderConnections() {
        var definition = new CopyNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var source = builder.AddNode(definition);
        var sink = builder.AddNode(definition);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        builder.AddConnection(source, definition.Out, sink, definition.In);
        var compiledSink = pipeline.Topology.Nodes.Single(node => node.ID == sink.ID);
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        context.Write(compiledSink, definition.In, 8);
        executor.Execute(context);
        Assert.Equal(8, context.Read<int>(compiledSink, definition.Out));
    }

    [Fact]
    public void Execute_InspectionCopiesCannotMutateCompiledGraphOrChannels() {
        var definition = new CopyNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var source = builder.AddNode(definition);
        var sink = builder.AddNode(definition);
        builder.AddConnection(source, definition.Out, sink, definition.In);
        var pipeline = builder.Build();
        pipeline.Topology.RemoveNode(source);
        var edges = pipeline.Edges;
        edges[0] = default;
        var executor = new PipelineExecutor(pipeline);
        var layers = executor.Layers;
        layers[0][0] = sink;
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        context.Write(source, definition.In, 17);
        executor.Execute(context);
        Assert.Equal(17, context.Read<int>(sink, definition.Out));
        Assert.Equal(2, pipeline.Topology.Nodes.Count());
        Assert.Equal(source.ID, pipeline.Edges[0].SourceNode.ID);
        Assert.False(pipeline.Topology.Nodes.First().Mappings is Dictionary<IPort, int>);
    }

    [Fact]
    public void AddNode_RegisteredDefinition_RejectsMetadataMutation() {
        var definition = new CopyNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        builder.AddNode(definition);
        Assert.Throws<InvalidOperationException>(() => definition.Input(new InputPort<int>("late", 0, new PassAllPortValidator<int>())));
        Assert.Throws<InvalidOperationException>(() => ((Port.Port) definition.In).IsRequired = false);
        Assert.False(definition.Inputs is List<Port.Port>);
    }

    [Fact]
    public void ForPipeline_Options_ExposesDeterministicCacheIdentity() {
        var pipeline = new PipelineBuilder(new NodeRegistry()).Build();
        var options = new RunOptions(8);
        var context = ArrayPipelineContext.ForPipeline(pipeline, options);
        Assert.Same(options, context.GetOptions<RunOptions>());
        Assert.Same(options, context.CacheIdentity);
        var projected = ArrayPipelineContext.ForPipeline(pipeline, new IdentityOptions(9));
        Assert.Equal("9", projected.CacheIdentity);
        Assert.Null(ArrayPipelineContext.ForPipeline(pipeline).CacheIdentity);
    }

    [Fact]
    public void Build_CastRules_AreSnapshottedAndImmutable() {
        var builder = new PipelineBuilder(new NodeRegistry());
        builder.RegisterCast<int, string>(TypeCast.Lossless, value => $"before:{value}");
        var pipeline = builder.Build();
        builder.RegisterCast<int, string>(TypeCast.Lossless, value => $"after:{value}");
        Assert.Equal("before:3", pipeline.CastRegistry.Cast<int, string>(3));
        Assert.Throws<InvalidOperationException>(() => pipeline.CastRegistry.Register<int, string>(TypeCast.Lossless, value => value.ToString()));
    }

    [Fact]
    public void Create_CustomContextConversions_AreIsolatedFromLaterRegistryChanges() {
        var registry = new CastRegistry();
        registry.Register<int, double>(TypeCast.Lossless, value => value + 1d);
        var first = ArrayPipelineContext.Create(registry, typeof(int));
        registry.Register<int, double>(TypeCast.Lossless, value => value + 2d);
        var second = ArrayPipelineContext.Create(registry, typeof(int));
        first.Write(0, 10);
        second.Write(0, 10);
        Assert.Equal(11d, first.Read<double>(0));
        Assert.Equal(12d, second.Read<double>(0));
    }

    [Fact]
    public void Read_NumericCastOnRead_DoesNotBox() {
        var context = ArrayPipelineContext.Create(typeof(int));
        context.Write(0, 42);
        for (var iteration = 0; iteration < 1000; iteration++) Assert.Equal(42d, context.Read<double>(0));
        var before = GC.GetAllocatedBytesForCurrentThread();
        double total = 0;
        for (var iteration = 0; iteration < 1000; iteration++) total += context.Read<double>(0);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(42000d, total);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(256)]
    public async Task ExecuteValueAsync_WarmedSynchronousTypedNodes_DoesNotAllocate(int nodeCount) {
        var definition = new CopyNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var nodes = Enumerable.Range(0, nodeCount).Select(_ => builder.AddNode(definition)).ToArray();
        for (var index = 1; index < nodes.Length; index++)
            builder.AddConnection(nodes[index - 1], definition.Out, nodes[index], definition.In);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        context.Write(nodes[0], definition.In, 42);
        for (var iteration = 0; iteration < 1000; iteration++) await executor.ExecuteValueAsync(context);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1000; iteration++) await executor.ExecuteValueAsync(context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(42, context.Read<int>(nodes[^1], definition.Out));
    }
}
