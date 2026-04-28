using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Serialization;
using ShironPipeline = Shiron.Lib.Pipeline.Pipeline;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class PipelineSerializationBenchmarks {
    [Params(5, 10, 25)]
    public int Size { get; set; }

    private ShironPipeline _linearPipeline;
    private ShironPipeline _complexPipeline;
    private NodeRegistry _registry = null!;
    private string _linearJson = null!;
    private string _complexJson = null!;

    [GlobalSetup]
    public void Setup() {
        _registry = new NodeRegistry();
        var identity = _registry.Register<IdentityNode>();
        var compute = _registry.Register<ComputeNode>();
        var merge = _registry.Register<MergeNode>();
        var multiOut = _registry.Register<MultiOutputNode>();

        {
            var builder = new PipelineBuilder(_registry);
            var prev = builder.AddNode(identity);
            for (int i = 1; i < Size * 5; i++) {
                var next = builder.AddNode(identity);
                builder.AddConnection(prev, identity.Out, next, identity.In);
                prev = next;
            }
            _linearPipeline = builder.Build();
        }

        {
            var builder = new PipelineBuilder(_registry);
            PipelineBuilder.NodeInstance? prevMerge = null;

            for (int block = 0; block < Size; block++) {
                var source = builder.AddNode(multiOut);
                if (prevMerge != null) {
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
            _complexPipeline = builder.Build();
        }

        _linearJson = _linearPipeline.Serialize();
        _complexJson = _complexPipeline.Serialize();
    }

    [Benchmark]
    public string Serialize_Linear() {
        return _linearPipeline.Serialize();
    }

    [Benchmark]
    public string Serialize_Complex() {
        return _complexPipeline.Serialize();
    }

    [Benchmark]
    public ShironPipeline Deserialize_Linear() {
        return PipelineSerialization.DeserializePipeline(_linearJson, _registry);
    }

    [Benchmark]
    public ShironPipeline Deserialize_Complex() {
        return PipelineSerialization.DeserializePipeline(_complexJson, _registry);
    }

    [Benchmark]
    public ShironPipeline RoundTrip_Linear() {
        return PipelineSerialization.DeserializePipeline(_linearPipeline.Serialize(), _registry);
    }

    [Benchmark]
    public ShironPipeline RoundTrip_Complex() {
        return PipelineSerialization.DeserializePipeline(_complexPipeline.Serialize(), _registry);
    }
}
