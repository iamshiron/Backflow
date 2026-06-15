using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

using PipelineBuilder = Shiron.Lib.Pipeline.PipelineBuilder;

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
    private Guid[] _directGuids = null!;
    private Dictionary<Type, int> _counts = null!;
    private Dictionary<Guid, int> _indices = null!;

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

        _ = builder.Build(out _counts, out _indices);

        // The pipeline only declares value-type (int) ports, so reference values
        // (e.g. the string benchmark) reuse an int-port index into the shared
        // object bucket. Size it to cover every assignable index.
        _counts[typeof(object)] = _counts[typeof(int)];

        _preloadedContext = CreateContext();
        for (var i = 0; i < PortCount; i++)
            _preloadedContext.Write(_instances[i], _passThrough.Input, i);
    }

    private ArrayPipelineContext CreateContext() {
        return new ArrayPipelineContext(CastRegistry.Default, _counts, _indices);
    }

    [Benchmark]
    public void Write_SingleInt() {
        var context = CreateContext();
        context.Write(_instances[0], _passThrough.Input, 42);
    }

    [Benchmark]
    public int Read_SingleInt() {
        return _preloadedContext.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public int Read_SingleIntMissing() {
        var context = CreateContext();
        return context.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public void WriteRead_SingleInt() {
        var context = CreateContext();
        context.Write(_instances[0], _passThrough.Input, 42);
        _ = context.Read<int>(_instances[0], _passThrough.Input);
    }

    [Benchmark]
    public void Write_SingleString() {
        var context = CreateContext();
        context.Write(_instances[0], _passThrough.Input, "hello benchmark");
    }

    [Benchmark]
    public void Write_ManyInts() {
        var context = CreateContext();
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
        var context = CreateContext();
        context.Write(_directGuids[0], 42);
    }

    [Benchmark]
    public int Read_DirectGuid() {
        return _preloadedContext.Read<int>(_directGuids[0]);
    }
}
