using System.Diagnostics;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline;

public class PipelineExecutor(Pipeline pipeline) {
    public PipelineBuilder.NodeInstance[][] Layers { get; } = pipeline.Topology.ToLayers();

    private readonly Dictionary<string, List<PipelineBuilder.EdgeInstance>> _incomingEdges = BuildIncomingEdges(pipeline.Edges);

    private static Dictionary<string, List<PipelineBuilder.EdgeInstance>> BuildIncomingEdges(PipelineBuilder.EdgeInstance[] edges) {
        var result = new Dictionary<string, List<PipelineBuilder.EdgeInstance>>();
        foreach (var edge in edges) {
            if (!result.TryGetValue(edge.DestinationNode.ID, out var list)) {
                list = [];
                result[edge.DestinationNode.ID] = list;
            }
            list.Add(edge);
        }
        return result;
    }

    private int TotalNodeCount => Layers.Sum(l => l.Length);

    public ExecutionStats Execute(IPipelineContext global) {
        var sw = Stopwatch.StartNew();
        int executed = 0, skipped = 0;

        foreach (var layer in Layers) {
            foreach (var node in layer) {
                if (ShouldSkipDueToPropagation(node)) {
                    node.State = NodeState.Skipped;
                    skipped++;
                    continue;
                }

                AssembleFrozenArrayInputs(node, global);
                ExecuteNodeAsync(node, global).GetAwaiter().GetResult();
                executed++;
            }
        }

        sw.Stop();
        return new ExecutionStats(TotalNodeCount, executed, skipped, sw.Elapsed);
    }

    public async Task<ExecutionStats> ExecuteAsync(IPipelineContext global) {
        var sw = Stopwatch.StartNew();
        int executed = 0, skipped = 0;

        foreach (var layer in Layers) {
            List<Task> tasks = [];
            foreach (var node in layer) {
                tasks.Add(Task.Run(async () => {
                    if (ShouldSkipDueToPropagation(node)) {
                        node.State = NodeState.Skipped;
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    AssembleFrozenArrayInputs(node, global);
                    await ExecuteNodeAsync(node, global);
                    Interlocked.Increment(ref executed);
                }));
            }
            await Task.WhenAll(tasks);
        }

        sw.Stop();
        return new ExecutionStats(TotalNodeCount, executed, skipped, sw.Elapsed);
    }

    private void AssembleFrozenArrayInputs(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return;

        var indexedByPort = new Dictionary<IPort, List<(int Index, Guid SourceGuid)>>();

        foreach (var edge in edges) {
            if (!edge.DestIndex.HasValue) continue;

            if (!indexedByPort.TryGetValue(edge.DestinationPort, out var list)) {
                list = [];
                indexedByPort[edge.DestinationPort] = list;
            }
            list.Add((edge.DestIndex.Value, edge.SourceNode.Mappings[edge.SourcePort]));
        }

        foreach (var (port, sources) in indexedByPort) {
            if (port is not IArrayInputPortMarker { IsFrozen: true }) continue;

            var targetGuid = node.Mappings[port];
            ((IArrayPortAssembly) port).Assemble(global, targetGuid, sources);
        }
    }

    private Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>> BuildIndexedInputs(PipelineBuilder.NodeInstance node) {
        var result = new Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>>();

        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return result;

        foreach (var edge in edges) {
            if (!edge.DestIndex.HasValue) continue;

            if (!result.TryGetValue(edge.DestinationPort, out var list)) {
                list = new List<(int Index, Guid SourceGuid)>();
                result[edge.DestinationPort] = list;
            }

            ((List<(int Index, Guid SourceGuid)>) list).Add((edge.DestIndex.Value, edge.SourceNode.Mappings[edge.SourcePort]));
        }

        return result;
    }

    private bool ShouldSkipDueToPropagation(PipelineBuilder.NodeInstance node) {
        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return false;

        foreach (var edge in edges) {
            if (edge.DestIndex.HasValue) continue;

            if (edge.SourceNode.State != NodeState.Skipped) continue;
            if (edge.DestinationPort is Port.Port { IsRequired: true }) return true;
        }

        return false;
    }

    private async Task ExecuteNodeAsync(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        var indexedInputs = BuildIndexedInputs(node);
        var context = new NodeContext(global, node.Mappings, indexedInputs);

        NodeState state;
        try {
            state = await node.Node.ExecuteAsync(context);
        } catch (Exception ex) {
            Console.WriteLine($"Error executing node {node.ID}: {ex.Message}");
            node.State = NodeState.Failed;
            throw new NodeExecutionException(node, ex);
        }

        node.State = state;
        if (state == NodeState.Failed) throw new NodeExecutionException(node);
    }
}

internal interface IArrayPortAssembly {
    void Assemble(IPipelineContext context, Guid targetGuid, IReadOnlyList<(int Index, Guid SourceGuid)> sources);
    void AssembleWithCount(IPipelineContext context, Guid targetGuid, IReadOnlyList<(int Index, Guid SourceGuid)> sources, int count);
}
