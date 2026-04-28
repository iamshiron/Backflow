using BenchmarkDotNet.Attributes;
using Shiron.Lib.Pipeline;
using ShironPipeline = Shiron.Lib.Pipeline.Pipeline;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class PipelineBuilderBenchmarks {
    [Params(5, 10, 25)]
    public int Size { get; set; }

    private NodeRegistry _registry = null!;
    private IdentityNode _identity = null!;
    private ComputeNode _compute = null!;
    private MergeNode _merge = null!;
    private MultiOutputNode _multiOut = null!;

    [GlobalSetup]
    public void Setup() {
        _registry = new NodeRegistry();
        _identity = _registry.Register<IdentityNode>();
        _compute = _registry.Register<ComputeNode>();
        _merge = _registry.Register<MergeNode>();
        _multiOut = _registry.Register<MultiOutputNode>();
    }

    [Benchmark]
    public ShironPipeline Build_LinearChain() {
        var builder = new PipelineBuilder(_registry);
        var prev = builder.AddNode(_identity);
        for (int i = 1; i < Size * 5; i++) {
            var next = builder.AddNode(_identity);
            builder.AddConnection(prev, _identity.Out, next, _identity.In);
            prev = next;
        }
        return builder.Build();
    }

    [Benchmark]
    public ShironPipeline Build_WideFanOut() {
        var builder = new PipelineBuilder(_registry);
        var source = builder.AddNode(_identity);
        for (int i = 0; i < Size * 5; i++) {
            var worker = builder.AddNode(_identity);
            builder.AddConnection(source, _identity.Out, worker, _identity.In);
        }
        return builder.Build();
    }

    [Benchmark]
    public ShironPipeline Build_Diamond() {
        var builder = new PipelineBuilder(_registry);
        var source = builder.AddNode(_multiOut);

        var c1 = builder.AddNode(_compute);
        var c2 = builder.AddNode(_compute);
        var c3 = builder.AddNode(_compute);

        builder.AddConnection(source, _multiOut.Out1, c1, _compute.InputA);
        builder.AddConnection(source, _multiOut.Out2, c1, _compute.InputB);
        builder.AddConnection(source, _multiOut.Out2, c2, _compute.InputA);
        builder.AddConnection(source, _multiOut.Out3, c2, _compute.InputB);
        builder.AddConnection(source, _multiOut.Out1, c3, _compute.InputA);
        builder.AddConnection(source, _multiOut.Out3, c3, _compute.InputB);

        var sink = builder.AddNode(_merge);
        builder.AddConnection(c1, _compute.Result, sink, _merge.InputA);
        builder.AddConnection(c2, _compute.Result, sink, _merge.InputB);
        builder.AddConnection(c3, _compute.Result, sink, _merge.InputC);

        return builder.Build();
    }

    [Benchmark]
    public ShironPipeline Build_ComplexGraph() {
        var builder = new PipelineBuilder(_registry);
        PipelineBuilder.NodeInstance? prevMerge = null;

        for (int block = 0; block < Size; block++) {
            var source = builder.AddNode(_multiOut);
            if (prevMerge != null) {
                builder.AddConnection(prevMerge.GetValueOrDefault(), _merge.Output, source, _multiOut.In);
            }

            var c1 = builder.AddNode(_compute);
            var c2 = builder.AddNode(_compute);
            var c3 = builder.AddNode(_compute);

            builder.AddConnection(source, _multiOut.Out1, c1, _compute.InputA);
            builder.AddConnection(source, _multiOut.Out2, c1, _compute.InputB);
            builder.AddConnection(source, _multiOut.Out2, c2, _compute.InputA);
            builder.AddConnection(source, _multiOut.Out3, c2, _compute.InputB);
            builder.AddConnection(source, _multiOut.Out1, c3, _compute.InputA);
            builder.AddConnection(source, _multiOut.Out3, c3, _compute.InputB);

            var m = builder.AddNode(_merge);
            builder.AddConnection(c1, _compute.Result, m, _merge.InputA);
            builder.AddConnection(c2, _compute.Result, m, _merge.InputB);
            builder.AddConnection(c3, _compute.Result, m, _merge.InputC);
            prevMerge = m;
        }

        return builder.Build();
    }
}
