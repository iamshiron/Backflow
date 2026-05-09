namespace Shiron.Lib.Pipeline.Exceptions;

/// <summary>Thrown when a node returns <c>false</c> or throws during execution.</summary>
public class NodeExecutionException(PipelineBuilder.NodeInstance nodeInstance, Exception? innerException = null)
    : Exception(
        innerException is not null
            ? $"Node '{nodeInstance.Node.GetType().Name}' ({nodeInstance.ID}) failed to execute: {innerException.Message}"
            : $"Node '{nodeInstance.Node.GetType().Name}' ({nodeInstance.ID}) failed to execute.",
        innerException
    ) {
    /// <summary>The node instance that failed.</summary>
    public PipelineBuilder.NodeInstance NodeInstance { get; } = nodeInstance;
}
