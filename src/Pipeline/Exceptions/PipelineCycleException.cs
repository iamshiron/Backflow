namespace Shiron.Lib.Pipeline.Exceptions;

/// <summary>Thrown when adding a connection would introduce a cycle in the DAG.</summary>
public class PipelineCycleException(PipelineBuilder.NodeInstance sourceNode, PipelineBuilder.NodeInstance destinationNode)
    : Exception($"Connection from '{sourceNode.Node.GetType().Name}' to '{destinationNode.Node.GetType().Name}' would create a cycle.") {
    /// <summary>The node the edge starts from.</summary>
    public PipelineBuilder.NodeInstance SourceNode { get; } = sourceNode;

    /// <summary>The node the edge points to.</summary>
    public PipelineBuilder.NodeInstance DestinationNode { get; } = destinationNode;
}
