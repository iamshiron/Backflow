using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

using PipelineBuilder = Shiron.Lib.Pipeline.PipelineBuilder;
using Pipe = global::Shiron.Lib.Pipeline.Pipeline;

namespace Shiron.Lib.Benchmarks.Pipeline;

/// <summary>
/// Counterpart to <see cref="PipelineContextBenchmarks"/> for the array-backed
/// <see cref="ArrayPipelineContext"/> (powered by <see cref="ArrayBucketStore"/>).
/// </summary>
[MemoryDiagnoser]
public class ArrayPipelineContextBenchmarks {
    [Params(1, 10, 100)]
    public int PortCount { get; set; }

    private NodeRegistry _registry = null!;
    private PassThroughNode _passThrough = null!;
    private PipelineBuilder.NodeInstance[] _instances = null!;
    private ArrayPipelineContext _preloadedContext = null!;
    private int[] _directChannels = null!;
    private Pipe _pipeline;

    [GlobalSetup]
    public void Setup() {
        _registry = BenchmarkRegistry.Create();
        _passThrough = _registry.Get<PassThroughNode>()!;

        var builder = new PipelineBuilder(_registry);
        _instances = new PipelineBuilder.NodeInstance[PortCount];
        _directChannels = new int[PortCount];

        for (var i = 0; i < PortCount; i++) {
            _instances[i] = builder.AddNode(_passThrough);
            _directChannels[i] = _instances[i].Mappings[_passThrough.Input];
        }

        _pipeline = builder.Build();

        _preloadedContext = ArrayPipelineContext.ForPipeline(_pipeline);
        for (var i = 0; i < PortCount; i++)
            _preloadedContext.Write(_instances[i], _passThrough.Input, i);
    }

    [Benchmark]
    public void Write_SingleInt() {
        var context = ArrayPipelineContext.ForPipeline(_pipeline);
        context.Write(_instances[0], _passThrough.Input, 42);
    }

    [Benchmark]
    public int Read_SingleInt() {
        return _preloadedContext.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public int Read_SingleIntMissing() {
        var context = ArrayPipelineContext.ForPipeline(_pipeline);
        return context.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public void WriteRead_SingleInt() {
        var context = ArrayPipelineContext.ForPipeline(_pipeline);
        context.Write(_instances[0], _passThrough.Input, 42);
        _ = context.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public void Write_SingleString() {
        var context = ArrayPipelineContext.ForPipeline(_pipeline);
        context.Write(_instances[0], _passThrough.Input, "hello benchmark");
    }

    [Benchmark]
    public void Write_ManyInts() {
        var context = ArrayPipelineContext.ForPipeline(_pipeline);
        for (var i = 0; i < PortCount; i++)
            context.Write(_instances[i], _passThrough.Input, i);
    }

    [Benchmark]
    public void Read_ManyInts() {
        for (var i = 0; i < PortCount; i++)
            _ = _preloadedContext.Read<int>(_instances[i], _passThrough.Input);
    }

    [Benchmark]
    public void Write_DirectChannel() {
        var context = ArrayPipelineContext.ForPipeline(_pipeline);
        context.Write(_directChannels[0], 42);
    }

    [Benchmark]
    public int Read_DirectChannel() {
        return _preloadedContext.Read<int>(_directChannels[0]);
    }
}
