using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline;

public class PipelineExecutor(Pipeline pipeline) {
    public PipelineBuilder.NodeInstance[][] Layers { get; } = pipeline.Topology.ToLayers();

    public void Execute(IPipelineContext global) {
        foreach (var layer in Layers) {
            foreach (var node in layer) {
                var context = new NodeContext(global, node.Mappings);

                Console.WriteLine($"Executing node {node.Node.GetType().Name} ({node.ID})...");
                node.Node.Execute(context);
            }
        }
    }
}
