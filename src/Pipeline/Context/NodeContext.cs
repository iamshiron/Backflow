namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Adapts <see cref="IPipelineContext"/> to <see cref="INodeContext"/> for a single node,
/// resolving each port to its mapped channel GUID.
/// </summary>
/// <param name="context">Global pipeline context to delegate reads/writes to.</param>
/// <param name="mappings">Port-to-channel-GUID mapping for this node.</param>
public class NodeContext(IPipelineContext context, IReadOnlyDictionary<Port.Port, Guid> mappings) : INodeContext {
    /// <inheritdoc/>
    public void Write(Port.Port port, object value) {
        context.Write(mappings[port], value);
    }
    /// <inheritdoc/>
    public object? Read(Port.Port port) {
        return context.Read(mappings[port]);
    }
}
