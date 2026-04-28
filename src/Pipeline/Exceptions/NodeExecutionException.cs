namespace Shiron.Lib.Pipeline.Exceptions;

public class NodeExecutionException(PipelineBuilder.NodeInstance nodeInstance, Exception? innerException = null)
    : Exception($"Node '{nodeInstance.Node.GetType().Name}' ({nodeInstance.ID}) failed to execute.", innerException) {
    public PipelineBuilder.NodeInstance NodeInstance { get; } = nodeInstance;
}
