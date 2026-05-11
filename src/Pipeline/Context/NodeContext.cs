using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

public class NodeContext(
    IPipelineContext context,
    IReadOnlyDictionary<IPort, Guid> mappings,
    IReadOnlyDictionary<IPort, Dictionary<int, Guid>> groupMappings
) : INodeContext {
    public NodeContext(IPipelineContext context, IReadOnlyDictionary<IPort, Guid> mappings)
        : this(context, mappings, new Dictionary<IPort, Dictionary<int, Guid>>()) { }

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

    public T? ReadGroup<T>(IPort group, int index) {
        return context.Read<T>(groupMappings[group][index]);
    }
    public void WriteGroup<T>(IPort group, int index, T? value) {
        context.Write(groupMappings[group][index], value);
    }
    public bool HasGroup<T>(IPort group, int index) {
        var guid = groupMappings[group][index];
        return context.Has<T>(guid);
    }
    public int GetGroupCount(IPort group) {
        return groupMappings.TryGetValue(group, out var map) ? map.Count : 0;
    }
}
