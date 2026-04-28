namespace Shiron.Lib.Pipeline.Exceptions;

public class PipelineCycleException(PipelineBuilder.NodeInstance sourceNode, PipelineBuilder.NodeInstance destinationNode)
    : Exception($"Connection from '{sourceNode.Node.GetType().Name}' to '{destinationNode.Node.GetType().Name}' would create a cycle.") {
    public PipelineBuilder.NodeInstance SourceNode { get; } = sourceNode;
    public PipelineBuilder.NodeInstance DestinationNode { get; } = destinationNode;
}
