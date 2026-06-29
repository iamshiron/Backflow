using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

using Pipe = global::Shiron.Lib.Pipeline.Pipeline;
using PipelineBuilder = global::Shiron.Lib.Pipeline.PipelineBuilder;
using PipelineExecutor = global::Shiron.Lib.Pipeline.PipelineExecutor;
using ExecutionStats = global::Shiron.Lib.Pipeline.ExecutionStats;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class PipelineExecutionBenchmarks {
    [Params(2, 5, 10, 20, 50)]
    public int NodeCount { get; set; }

    private NodeRegistry _registry = null!;
    private PassThroughNode _passThrough = null!;

    private Pipe _serialPipeline;
    private PipelineExecutor _serialExecutor = null!;
    private PipelineBuilder.NodeInstance _serialFirst = null!;

    private Pipe _fanOutPipeline;
    private PipelineExecutor _fanOutExecutor = null!;
    private PipelineBuilder.NodeInstance _fanOutSource = null!;

    private Pipe _fanOutSerialPipeline;
    private PipelineExecutor _fanOutSerialExecutor = null!;
    private PipelineBuilder.NodeInstance _fanOutSerialSource = null!;

    private Pipe _binaryOutPipeline;
    private PipelineExecutor _binaryOutExecutor = null!;
    private PipelineBuilder.NodeInstance _binaryOutRoot = null!;

    [GlobalSetup]
    public void Setup() {
        _registry = BenchmarkRegistry.Create();
        _passThrough = _registry.Get<PassThroughNode>()!;

        BuildSerial();
        BuildFanOut();
        BuildFanOutToSerial();
        BuildBinaryOut();
    }

    private void BuildSerial() {
        var builder = new PipelineBuilder(_registry);
        var instances = new PipelineBuilder.NodeInstance[NodeCount];

        for (var i = 0; i < NodeCount; i++)
            instances[i] = builder.AddNode(_passThrough);

        for (var i = 0; i < NodeCount - 1; i++)
            builder.AddConnection(instances[i], _passThrough.Output, instances[i + 1], _passThrough.Input);

        _serialPipeline = builder.Build();
        _serialExecutor = new PipelineExecutor(_serialPipeline);
        _serialFirst = instances[0];
    }

    private void BuildFanOut() {
        var builder = new PipelineBuilder(_registry);
        var source = builder.AddNode(_passThrough);

        for (var i = 0; i < NodeCount; i++) {
            var target = builder.AddNode(_passThrough);
            builder.AddConnection(source, _passThrough.Output, target, _passThrough.Input);
        }

        _fanOutPipeline = builder.Build();
        _fanOutExecutor = new PipelineExecutor(_fanOutPipeline);
        _fanOutSource = source;
    }

    private void BuildFanOutToSerial() {
        const int serialDepth = 3;
        var builder = new PipelineBuilder(_registry);
        var source = builder.AddNode(_passThrough);

        for (var b = 0; b < NodeCount; b++) {
            var prev = source;
            for (var d = 0; d < serialDepth; d++) {
                var next = builder.AddNode(_passThrough);
                builder.AddConnection(prev, _passThrough.Output, next, _passThrough.Input);
                prev = next;
            }
        }

        _fanOutSerialPipeline = builder.Build();
        _fanOutSerialExecutor = new PipelineExecutor(_fanOutSerialPipeline);
        _fanOutSerialSource = source;
    }

    private void BuildBinaryOut() {
        var depth = NodeCount switch {
            2 => 2,
            5 => 3,
            10 => 4,
            20 => 5,
            _ => 6
        };

        var builder = new PipelineBuilder(_registry);
        var root = builder.AddNode(_passThrough);
        var currentLevel = new List<PipelineBuilder.NodeInstance> { root };

        for (var level = 1; level < depth; level++) {
            var nextLevel = new List<PipelineBuilder.NodeInstance>();
            foreach (var parent in currentLevel) {
                var child0 = builder.AddNode(_passThrough);
                var child1 = builder.AddNode(_passThrough);
                builder.AddConnection(parent, _passThrough.Output, child0, _passThrough.Input);
                builder.AddConnection(parent, _passThrough.Output, child1, _passThrough.Input);
                nextLevel.Add(child0);
                nextLevel.Add(child1);
            }
            currentLevel = nextLevel;
        }

        _binaryOutPipeline = builder.Build();
        _binaryOutExecutor = new PipelineExecutor(_binaryOutPipeline);
        _binaryOutRoot = root;
    }

    [Benchmark]
    public ExecutionStats Execute_Serial() {
        var context = ArrayPipelineContext.ForPipeline(_serialPipeline);
        context.Write(_serialFirst, _passThrough.Input, 42);
        return _serialExecutor.Execute(context);
    }

    [Benchmark]
    public ExecutionStats Execute_FanOut() {
        var context = ArrayPipelineContext.ForPipeline(_fanOutPipeline);
        context.Write(_fanOutSource, _passThrough.Input, 42);
        return _fanOutExecutor.Execute(context);
    }

    [Benchmark]
    public ExecutionStats Execute_FanOutToSerial() {
        var context = ArrayPipelineContext.ForPipeline(_fanOutSerialPipeline);
        context.Write(_fanOutSerialSource, _passThrough.Input, 42);
        return _fanOutSerialExecutor.Execute(context);
    }

    [Benchmark]
    public ExecutionStats Execute_BinaryOut() {
        var context = ArrayPipelineContext.ForPipeline(_binaryOutPipeline);
        context.Write(_binaryOutRoot, _passThrough.Input, 42);
        return _binaryOutExecutor.Execute(context);
    }
}
