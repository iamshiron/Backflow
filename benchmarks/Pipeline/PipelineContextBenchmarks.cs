using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

using PipelineBuilder = Shiron.Lib.Pipeline.PipelineBuilder;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class PipelineContextBenchmarks {
    [Params(1, 10, 100)]
    public int PortCount { get; set; }

    private NodeRegistry _registry = null!;
    private PassThroughNode _passThrough = null!;
    private PipelineBuilder.NodeInstance[] _instances = null!;
    private PipelineContext _preloadedContext = null!;
    private Guid[] _directGuids = null!;

    [GlobalSetup]
    public void Setup() {
        _registry = BenchmarkRegistry.Create();
        _passThrough = _registry.Get<PassThroughNode>()!;

        var builder = new PipelineBuilder(_registry);
        _instances = new PipelineBuilder.NodeInstance[PortCount];
        _directGuids = new Guid[PortCount];

        for (var i = 0; i < PortCount; i++) {
            _instances[i] = builder.AddNode(_passThrough);
            _directGuids[i] = _instances[i].Mappings[_passThrough.Input];
        }

        _ = builder.Build();

        _preloadedContext = new PipelineContext();
        for (var i = 0; i < PortCount; i++)
            _preloadedContext.Write(_instances[i], _passThrough.Input, i);
    }

    [Benchmark]
    public void Write_SingleInt() {
        var context = new PipelineContext();
        context.Write(_instances[0], _passThrough.Input, 42);
    }

    [Benchmark]
    public int Read_SingleInt() {
        return _preloadedContext.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public int Read_SingleIntMissing() {
        var context = new PipelineContext();
        return context.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public void WriteRead_SingleInt() {
        var context = new PipelineContext();
        context.Write(_instances[0], _passThrough.Input, 42);
        _ = context.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public void Write_SingleString() {
        var context = new PipelineContext();
        context.Write(_instances[0], _passThrough.Input, "hello benchmark");
    }

    [Benchmark]
    public void Write_ManyInts() {
        var context = new PipelineContext();
        for (var i = 0; i < PortCount; i++)
            context.Write(_instances[i], _passThrough.Input, i);
    }

    [Benchmark]
    public void Read_ManyInts() {
        for (var i = 0; i < PortCount; i++)
            _ = _preloadedContext.Read<int>(_instances[i], _passThrough.Input);
    }

    [Benchmark]
    public void Write_DirectGuid() {
        var context = new PipelineContext();
        context.Write(_directGuids[0], 42);
    }

    [Benchmark]
    public int Read_DirectGuid() {
        return _preloadedContext.Read<int>(_directGuids[0]);
    }
}
