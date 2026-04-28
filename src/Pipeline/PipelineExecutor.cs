using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline;

public class PipelineExecutor(Pipeline pipeline) {
    public PipelineBuilder.NodeInstance[][] Layers { get; } = pipeline.Topology.ToLayers();

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
