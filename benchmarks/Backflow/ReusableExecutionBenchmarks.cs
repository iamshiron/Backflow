using BenchmarkDotNet.Attributes;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Validator;
using Shiron.Backflow.Registry;

namespace Shiron.Backflow.Benchmarks;

/// <summary>Measures warmed execution separately from graph compilation and context creation.</summary>
[MemoryDiagnoser]
public class ReusableExecutionBenchmarks {
    /// <summary>Number of shared-definition nodes in a serial graph.</summary>
    [Params(1, 32, 256)]
    public int NodeCount { get; set; }

    private PipelineExecutor _executor = null!;
    private ArrayPipelineContext _context = null!;

    /// <summary>Builds once and warms the context-owned node adapters.</summary>
    [GlobalSetup]
    public void Setup() {
        var node = new CopyNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var nodes = Enumerable.Range(0, NodeCount).Select(_ => builder.AddNode(node)).ToArray();
        for (var index = 1; index < nodes.Length; index++)
            builder.AddConnection(nodes[index - 1], node.Out, nodes[index], node.In);
        var pipeline = builder.Build();
        _executor = new PipelineExecutor(pipeline);
        _context = ArrayPipelineContext.ForPipeline(pipeline);
        _context.Write(nodes[0], node.In, 42);
        _executor.Execute(_context);
    }

    /// <summary>Measures the synchronous API.</summary>
    [Benchmark(Baseline = true)]
    public ExecutionStats Synchronous() => _executor.Execute(_context);

    /// <summary>Measures the synchronous-completion async API without allocating a result task.</summary>
    [Benchmark]
    public ValueTask<ExecutionStats> ValueTask() => _executor.ExecuteValueAsync(_context);

    /// <summary>Measures the compatibility API, including its result task allocation.</summary>
    [Benchmark]
    public Task<ExecutionStats> Task() => _executor.ExecuteAsync(_context);

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
}
