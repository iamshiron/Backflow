using System.Diagnostics;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;

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

    public ExecutionStats Execute(IPipelineContext global, INodeCache? cache = null, bool invalidateCache = false) {
        var sw = Stopwatch.StartNew();
        int executed = 0, skipped = 0, hits = 0, misses = 0, updates = 0;

        foreach (var layer in Layers) {
            foreach (var node in layer) {
                if (ShouldSkipDueToPropagation(node)) {
                    node.State = NodeState.Skipped;
                    skipped++;
                    continue;
                }

                if (ShouldSkipCaching(cache, node)) {
                    ExecuteNode(node, global);
                    executed++;
                    continue;
                }

                var context = new NodeContext(global, node.Mappings);
                var inputs = ReadInputs(node, context);
                var key = CacheKey.Create(node.Node, inputs);

                if (!invalidateCache) {
                    var cached = cache!.Get(key).GetAwaiter().GetResult();
                    if (cached is not null) {
                        RestoreOutputs(node, context, cached);
                        node.State = NodeState.Skipped;
                        skipped++;
                        hits++;
                        continue;
                    }
                }

                misses++;
                ExecuteNode(node, global);
                executed++;
                updates++;

                var entry = CaptureEntry(node, context, inputs);
                cache!.Set(key, entry).GetAwaiter().GetResult();
            }
        }

        sw.Stop();
        return new ExecutionStats(TotalNodeCount, executed, skipped, hits, misses, updates, sw.Elapsed);
    }

    public async Task<ExecutionStats> ExecuteAsync(IPipelineContext global, INodeCache? cache = null, bool invalidateCache = false) {
        var sw = Stopwatch.StartNew();
        int executed = 0, skipped = 0, hits = 0, misses = 0, updates = 0;

        foreach (var layer in Layers) {
            List<Task> tasks = [];
            foreach (var node in layer) {
                tasks.Add(Task.Run(async () => {
                    if (ShouldSkipDueToPropagation(node)) {
                        node.State = NodeState.Skipped;
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    if (ShouldSkipCaching(cache, node)) {
                        ExecuteNode(node, global);
                        Interlocked.Increment(ref executed);
                        return;
                    }

                    var context = new NodeContext(global, node.Mappings);
                    var inputs = ReadInputs(node, context);
                    var key = CacheKey.Create(node.Node, inputs);

                    if (!invalidateCache) {
                        var cached = await cache!.Get(key);
                        if (cached is not null) {
                            RestoreOutputs(node, context, cached);
                            node.State = NodeState.Skipped;
                            Interlocked.Increment(ref skipped);
                            Interlocked.Increment(ref hits);
                            return;
                        }
                    }

                    Interlocked.Increment(ref misses);
                    ExecuteNode(node, global);
                    Interlocked.Increment(ref executed);
                    Interlocked.Increment(ref updates);

                    var entry = CaptureEntry(node, context, inputs);
                    await cache!.Set(key, entry);
                }));
            }
            await Task.WhenAll(tasks);
        }

        sw.Stop();
        return new ExecutionStats(TotalNodeCount, executed, skipped, hits, misses, updates, sw.Elapsed);
    }

    private static bool ShouldSkipCaching(INodeCache? cache, PipelineBuilder.NodeInstance node) {
        return cache is null || !node.Node.UseCache;
    }

    private bool ShouldSkipDueToPropagation(PipelineBuilder.NodeInstance node) {
        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return false;

        foreach (var edge in edges) {
            if (edge.SourceNode.State != NodeState.Skipped) continue;
            if (edge.DestinationPort is Port.Port { IsRequired: true }) return true;
        }

        return false;
    }

    private static void ExecuteNode(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        var context = new NodeContext(global, node.Mappings);

        NodeState state;
        try {
            state = node.Node.ExecuteAsync(context).GetAwaiter().GetResult();
        } catch (Exception ex) {
            node.State = NodeState.Failed;
            throw new NodeExecutionException(node, ex);
        }

        node.State = state;
        if (state == NodeState.Failed) throw new NodeExecutionException(node);
    }

    private static List<(string PortName, Type Type, object? Value)> ReadInputs(
        PipelineBuilder.NodeInstance node,
        INodeContext context
    ) {
        var inputs = new List<(string PortName, Type Type, object? Value)>(node.Node.Inputs.Count);

        foreach (var port in node.Node.Inputs) {
            var portType = port.GetType().GetGenericArguments()[0];
            var value = context.ReadAny(port);
            inputs.Add((port.Name, portType, value));
        }

        return inputs;
    }

    private static void RestoreOutputs(
        PipelineBuilder.NodeInstance node,
        INodeContext context,
        CacheEntry cached
    ) {
        foreach (var port in node.Node.Outputs) {
            if (!cached.HasOutput(port.Name)) continue;
            var value = cached.GetOutputAny(port.Name);
            context.Write(port, value);
        }
    }

    private static CacheEntry CaptureEntry(
        PipelineBuilder.NodeInstance node,
        INodeContext context,
        List<(string PortName, Type Type, object? Value)> inputs
    ) {
        var entry = new CacheEntry();

        foreach (var (portName, type, value) in inputs) {
            entry.AddInput(portName, type, value);
        }

        foreach (var port in node.Node.Outputs) {
            if (!context.HasAny(port)) continue;
            var portType = port.GetType().GetGenericArguments()[0];
            var value = context.ReadAny(port);
            entry.AddOutput(port.Name, portType, value);
        }

        return entry;
    }
}
