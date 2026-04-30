using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class LinearChainExecutionBenchmarks {
    [Params(10, 50, 100)]
    public int Size { get; set; }

    private PipelineExecutor _executor = null!;
    private PipelineBuilder.NodeInstance _firstNode;
    private Port _firstInput = null!;

    [GlobalSetup]
    public void Setup() {
        var registry = new NodeRegistry();
        var identity = registry.Register<IdentityNode>();
        var builder = new PipelineBuilder(registry);

        _firstNode = builder.AddNode(identity);
        _firstInput = identity.In;
        var prev = _firstNode;

        for (int i = 1; i < Size; i++) {
            var next = builder.AddNode(identity);
            builder.AddConnection(prev, identity.Out, next, identity.In);
            prev = next;
        }

        _executor = new PipelineExecutor(builder.Build());
    }

    private static PipelineContext CreateContext(
        PipelineBuilder.NodeInstance node, Port port, int value) {
        var ctx = new PipelineContext();
        ctx.Write(node, port, value);
        return ctx;
    }

    [Benchmark]
    public void Execute() {
        _executor.Execute(CreateContext(_firstNode, _firstInput, 42));
    }

    [Benchmark]
    public Task ExecuteAsync() {
        return _executor.ExecuteAsync(CreateContext(_firstNode, _firstInput, 42));
    }
}

[MemoryDiagnoser]
public class WideFanOutExecutionBenchmarks {
    [Params(10, 50, 100)]
    public int Size { get; set; }

    private PipelineExecutor _executor = null!;
    private PipelineBuilder.NodeInstance _sourceNode;
    private Port _sourceInput = null!;

    [GlobalSetup]
    public void Setup() {
        var registry = new NodeRegistry();
        var identity = registry.Register<IdentityNode>();
        var builder = new PipelineBuilder(registry);

        _sourceNode = builder.AddNode(identity);
        _sourceInput = identity.In;

        for (int i = 0; i < Size; i++) {
            var worker = builder.AddNode(identity);
            builder.AddConnection(_sourceNode, identity.Out, worker, identity.In);
        }

        _executor = new PipelineExecutor(builder.Build());
    }

    private static PipelineContext CreateContext(
        PipelineBuilder.NodeInstance node, Port port, int value) {
        var ctx = new PipelineContext();
        ctx.Write(node, port, value);
        return ctx;
    }

    [Benchmark]
    public void Execute() {
        _executor.Execute(CreateContext(_sourceNode, _sourceInput, 42));
    }

    [Benchmark]
    public Task ExecuteAsync() {
        return _executor.ExecuteAsync(CreateContext(_sourceNode, _sourceInput, 42));
    }
}

[MemoryDiagnoser]
public class ComplexGraphExecutionBenchmarks {
    [Params(5, 10, 25)]
    public int Size { get; set; }

    private PipelineExecutor _executor = null!;
    private PipelineBuilder.NodeInstance _firstNode;
    private Port _firstInput = null!;

    [GlobalSetup]
    public void Setup() {
        var registry = new NodeRegistry();
        var multiOut = registry.Register<MultiOutputNode>();
        var compute = registry.Register<ComputeNode>();
        var merge = registry.Register<MergeNode>();
        var builder = new PipelineBuilder(registry);

        PipelineBuilder.NodeInstance? prevMerge = null;

        for (int block = 0; block < Size; block++) {
            var source = builder.AddNode(multiOut);
            if (prevMerge == null) {
                _firstNode = source;
                _firstInput = multiOut.In;
            } else {
                builder.AddConnection(prevMerge.GetValueOrDefault(), merge.Output, source, multiOut.In);
            }

            var c1 = builder.AddNode(compute);
            var c2 = builder.AddNode(compute);
            var c3 = builder.AddNode(compute);

            builder.AddConnection(source, multiOut.Out1, c1, compute.InputA);
            builder.AddConnection(source, multiOut.Out2, c1, compute.InputB);
            builder.AddConnection(source, multiOut.Out2, c2, compute.InputA);
            builder.AddConnection(source, multiOut.Out3, c2, compute.InputB);
            builder.AddConnection(source, multiOut.Out1, c3, compute.InputA);
            builder.AddConnection(source, multiOut.Out3, c3, compute.InputB);

            var m = builder.AddNode(merge);
            builder.AddConnection(c1, compute.Result, m, merge.InputA);
            builder.AddConnection(c2, compute.Result, m, merge.InputB);
            builder.AddConnection(c3, compute.Result, m, merge.InputC);
            prevMerge = m;
        }

        _executor = new PipelineExecutor(builder.Build());
    }

    private static PipelineContext CreateContext(
        PipelineBuilder.NodeInstance node, Port port, int value) {
        var ctx = new PipelineContext();
        ctx.Write(node, port, value);
        return ctx;
    }

    [Benchmark]
    public void Execute() {
        _executor.Execute(CreateContext(_firstNode, _firstInput, 42));
    }

    [Benchmark]
    public Task ExecuteAsync() {
        return _executor.ExecuteAsync(CreateContext(_firstNode, _firstInput, 42));
    }
}
