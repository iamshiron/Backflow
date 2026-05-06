using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Adapts <see cref="IPipelineContext"/> to <see cref="INodeContext"/> for a single node,
/// resolving each port to its mapped channel GUID.
/// </summary>
/// <param name="context">Global pipeline context to delegate reads/writes to.</param>
/// <param name="mappings">Port-to-channel-GUID mapping for this node.</param>
public class NodeContext(IPipelineContext context, IReadOnlyDictionary<IPort, Guid> mappings) : INodeContext {
    public void Write<T>(IPort port, T? value) {
        context.Write<T>(mappings[port], value);
    }
    public T? Read<T>(IPort port) {
        return context.Read<T>(mappings[port]);
    }
    public void Write(IPort port, object? value) {
        context.Write(mappings[port], value);
    }
    public object? ReadAny(IPort port) {
        return context.ReadAny(mappings[port]);
    }
    public bool Has<T>(IPort port) {
        return context.Has<T>(mappings[port]);
    }
    public bool HasAny(IPort port) {
        return context.HasAny(mappings[port]);
    }
}
