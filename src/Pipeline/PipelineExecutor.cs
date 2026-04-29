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
    /// <exception cref="NodeExecutionException">A node returned <c>false</c> or threw.</exception>
    public void Execute(IPipelineContext global) {
        foreach (var layer in Layers) {
            foreach (var node in layer) {
                var context = new NodeContext(global, node.Mappings);

                bool success;
                try {
                    success = node.Node.Execute(context).GetAwaiter().GetResult();
                } catch (Exception ex) {
                    throw new NodeExecutionException(node, ex);
                }

                if (!success) throw new NodeExecutionException(node);
            }
        }
    }

    /// <summary>Execute all nodes asynchronously. Nodes within the same layer run in parallel.</summary>
    /// <param name="global">Shared context for inter-node data exchange.</param>
    /// <exception cref="NodeExecutionException">A node returned <c>false</c> or threw.</exception>
    public async Task ExecuteAsync(IPipelineContext global) {
        foreach (var layer in Layers) {
            List<Task> tasks = [];
            foreach (var node in layer) {
                tasks.Add(Task.Run(async () => {
                    var context = new NodeContext(global, node.Mappings);

                    bool success;
                    try {
                        success = await node.Node.Execute(context);
                    } catch (Exception ex) {
                        throw new NodeExecutionException(node, ex);
                    }

                    if (!success) throw new NodeExecutionException(node);
                }));
            }
            await Task.WhenAll(tasks);
        }
    }
}
