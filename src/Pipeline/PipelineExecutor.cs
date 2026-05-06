using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Executes a <see cref="Pipeline"/> by topologically sorting nodes into layers
/// and running each layer sequentially (sync) or in parallel (async).
/// </summary>
/// <param name="pipeline">Pipeline topology to execute.</param>
public class PipelineExecutor(Pipeline pipeline) {
    /// <summary>Topologically sorted layers of node instances.</summary>
    public PipelineBuilder.NodeInstance[][] Layers { get; } = pipeline.Topology.ToLayers();

    /// <summary>Execute all nodes synchronously, layer by layer.</summary>
    /// <param name="global">Shared context for inter-node data exchange.</param>
    /// <param name="cache">Optional caching strategy. <c>null</c> disables caching entirely.</param>
    /// <param name="invalidateCache">When <c>true</c>, skip cache lookups but still write results to cache.</param>
    /// <exception cref="NodeExecutionException">A node returned <c>false</c> or threw.</exception>
    public void Execute(IPipelineContext global, INodeCache? cache = null, bool invalidateCache = false) {
        foreach (var layer in Layers) {
            foreach (var node in layer) {
                if (ShouldSkipCaching(cache, node)) {
                    ExecuteNode(node, global);
                    continue;
                }

                var context = new NodeContext(global, node.Mappings);
                var inputs = ReadInputs(node, context);
                var key = CacheKey.Create(node.Node, inputs);

                if (!invalidateCache) {
                    var cached = cache!.Get(key).GetAwaiter().GetResult();
                    if (cached is not null) {
                        RestoreOutputs(node, context, cached);
                        continue;
                    }
                }

                ExecuteNode(node, global);

                var entry = CaptureEntry(node, context, inputs);
                cache!.Set(key, entry).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>Execute all nodes asynchronously. Nodes within the same layer run in parallel.</summary>
    /// <param name="global">Shared context for inter-node data exchange.</param>
    /// <param name="cache">Optional caching strategy. <c>null</c> disables caching entirely.</param>
    /// <param name="invalidateCache">When <c>true</c>, skip cache lookups but still write results to cache.</param>
    /// <exception cref="NodeExecutionException">A node returned <c>false</c> or threw.</exception>
    public async Task ExecuteAsync(IPipelineContext global, INodeCache? cache = null, bool invalidateCache = false) {
        foreach (var layer in Layers) {
            List<Task> tasks = [];
            foreach (var node in layer) {
                tasks.Add(Task.Run(async () => {
                    if (ShouldSkipCaching(cache, node)) {
                        ExecuteNode(node, global);
                        return;
                    }

                    var context = new NodeContext(global, node.Mappings);
                    var inputs = ReadInputs(node, context);
                    var key = CacheKey.Create(node.Node, inputs);

                    if (!invalidateCache) {
                        var cached = await cache!.Get(key);
                        if (cached is not null) {
                            RestoreOutputs(node, context, cached);
                            return;
                        }
                    }

                    ExecuteNode(node, global);

                    var entry = CaptureEntry(node, context, inputs);
                    await cache!.Set(key, entry);
                }));
            }
            await Task.WhenAll(tasks);
        }
    }

    private static bool ShouldSkipCaching(INodeCache? cache, PipelineBuilder.NodeInstance node) {
        return cache is null || !node.Node.UseCache;
    }

    private static void ExecuteNode(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        var context = new NodeContext(global, node.Mappings);

        bool success;
        try {
            success = node.Node.Execute(context).GetAwaiter().GetResult();
        } catch (Exception ex) {
            throw new NodeExecutionException(node, ex);
        }

        if (!success) throw new NodeExecutionException(node);
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
