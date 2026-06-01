using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Shiron.Lib.Pipeline.Serialization;

using Pipe = global::Shiron.Lib.Pipeline.Pipeline;
using PipelineBuilder = global::Shiron.Lib.Pipeline.PipelineBuilder;
using PipelineExecutor = global::Shiron.Lib.Pipeline.PipelineExecutor;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class PipelineBuilderBenchmarks {
    [Params(2, 5, 10, 20, 50)]
    public int NodeCount { get; set; }

    private NodeRegistry _registry = null!;
    private PassThroughNode _passThrough = null!;

    private Pipe _serialPipeline;
    private Pipe _fanOutPipeline;
    private string _serializedDefinition = null!;
    private string _serializedInputs = null!;
    private PipelineContext _serializedContext = null!;

    [GlobalSetup]
    public void Setup() {
        _registry = BenchmarkRegistry.Create();
        _passThrough = _registry.Get<PassThroughNode>()!;

        _serialPipeline = BuildSerialPipeline();
        _fanOutPipeline = BuildFanOutPipeline();

        _serializedContext = new PipelineContext();
        _serializedContext.Write(
            _serialPipeline.Topology.Nodes.First(),
            _passThrough.Input,
            42
        );

        _serializedDefinition = _serialPipeline.SerializeDefinition();
        _serializedInputs = _serialPipeline.SerializeInputs(_serializedContext);
    }

    private Pipe BuildSerialPipeline() {
        var builder = new PipelineBuilder(_registry);
        var instances = new PipelineBuilder.NodeInstance[NodeCount];

        for (var i = 0; i < NodeCount; i++)
            instances[i] = builder.AddNode(_passThrough);

        for (var i = 0; i < NodeCount - 1; i++)
            builder.AddConnection(instances[i], _passThrough.Output, instances[i + 1], _passThrough.Input);

        return builder.Build();
    }

    private Pipe BuildFanOutPipeline() {
        var builder = new PipelineBuilder(_registry);
        var source = builder.AddNode(_passThrough);

        for (var i = 0; i < NodeCount; i++) {
            var target = builder.AddNode(_passThrough);
            builder.AddConnection(source, _passThrough.Output, target, _passThrough.Input);
        }

        return builder.Build();
    }

    [Benchmark]
    public Pipe Build_Serial() {
        return BuildSerialPipeline();
    }

    [Benchmark]
    public Pipe Build_FanOut() {
        return BuildFanOutPipeline();
    }

    [Benchmark]
    public string SerializeDefinition() {
        return _serialPipeline.SerializeDefinition();
    }

    [Benchmark]
    public string SerializeInputs() {
        return _serialPipeline.SerializeInputs(_serializedContext);
    }

    [Benchmark]
    public Pipe DeserializeDefinition() {
        return PipelineSerialization.DeserializeDefinition(_serializedDefinition, _registry);
    }

    [Benchmark]
    public PipelineContext DeserializeInputs() {
        return PipelineSerialization.DeserializeInputs(_serializedInputs, _serialPipeline);
    }

    [Benchmark]
    public PipelineExecutor LoadExecutor() {
        return new PipelineExecutor(_serialPipeline);
    }
}
