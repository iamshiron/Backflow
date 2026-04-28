using BenchmarkDotNet.Attributes;
using Shiron.Lib.Collections;

namespace Shiron.Lib.Benchmarks.Pipeline;

[MemoryDiagnoser]
public class DagBenchmarks {
    [Params(10, 100, 500)]
    public int Size { get; set; }

    private DirectedAcyclicGraph<int> _graph = null!;

    [GlobalSetup]
    public void Setup() {
        _graph = new DirectedAcyclicGraph<int>();
        for (int i = 0; i < Size; i++) _graph.AddNode(i);
        for (int i = 0; i < Size - 1; i++) {
            _graph.AddEdge(i, i + 1);
            if (i + 2 < Size) _graph.AddEdge(i, i + 2);
        }
    }

    [Benchmark]
    public DirectedAcyclicGraph<int> AddNodes() {
        var dag = new DirectedAcyclicGraph<int>();
        for (int i = 0; i < Size; i++) dag.AddNode(i);
        return dag;
    }

    [Benchmark]
    public DirectedAcyclicGraph<int> AddEdges_Linear() {
        var dag = new DirectedAcyclicGraph<int>();
        for (int i = 0; i < Size; i++) dag.AddNode(i);
        for (int i = 0; i < Size - 1; i++) dag.AddEdge(i, i + 1);
        return dag;
    }

    [Benchmark]
    public DirectedAcyclicGraph<int> AddEdges_Skip() {
        var dag = new DirectedAcyclicGraph<int>();
        for (int i = 0; i < Size; i++) dag.AddNode(i);
        for (int i = 0; i < Size - 2; i++) dag.AddEdge(i, i + 2);
        return dag;
    }

    [Benchmark]
    public List<int> TopologicalSort() {
        return _graph.TopologicalSort().ToList();
    }

    [Benchmark]
    public int[][] ToLayers() {
        return _graph.ToLayers();
    }

    [Benchmark]
    public void RemoveEdges() {
        var dag = new DirectedAcyclicGraph<int>();
        for (int i = 0; i < Size; i++) dag.AddNode(i);
        for (int i = 0; i < Size - 1; i++) dag.AddEdge(i, i + 1);
        for (int i = 0; i < Size - 1; i++) dag.RemoveEdge(i, i + 1);
    }

    [Benchmark]
    public void RemoveNodes() {
        var dag = new DirectedAcyclicGraph<int>();
        for (int i = 0; i < Size; i++) dag.AddNode(i);
        for (int i = 0; i < Size - 1; i++) dag.AddEdge(i, i + 1);
        for (int i = 0; i < Size; i++) dag.RemoveNode(i);
    }
}
