namespace Shiron.Lib.Pipeline.Exceptions;

/// <summary>Thrown when a connection would introduce a cycle in the DAG.</summary>
public class PipelineCycleException : Exception {
    public PipelineBuilder.NodeInstance? SourceNode { get; }
    public PipelineBuilder.NodeInstance? DestinationNode { get; }
    public string? SourceId { get; }
    public string? DestinationId { get; }

    public PipelineCycleException(PipelineBuilder.NodeInstance sourceNode, PipelineBuilder.NodeInstance destinationNode)
        : base($"Connection from '{sourceNode.Node.GetType().Name}' to '{destinationNode.Node.GetType().Name}' would create a cycle.") {
        SourceNode = sourceNode;
        DestinationNode = destinationNode;
    }

    public PipelineCycleException(string sourceId, string destinationId)
        : base($"Connection from '{sourceId}' to '{destinationId}' would create a cycle.") {
        SourceId = sourceId;
        DestinationId = destinationId;
    }
}
