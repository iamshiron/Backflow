using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

public class NodeContext(
    IPipelineContext context,
    IReadOnlyDictionary<IPort, Guid> mappings,
    IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>> indexedInputs
) : INodeContext {
    public NodeContext(IPipelineContext context, IReadOnlyDictionary<IPort, Guid> mappings)
        : this(context, mappings, new Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>>()) {
    }

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

    public void InitializeArray<T>(IArrayInputPort<T> port) {
        var targetGuid = mappings[(IPort) port];

        if (indexedInputs.TryGetValue((IPort) port, out var sources) && sources.Count > 0) {
            var count = sources.Max(s => s.Index) + 1;
            ((IArrayPortAssembly) port).AssembleWithCount(context, targetGuid, sources, count);
        } else {
            throw new InvalidOperationException(
                $"Cannot initialize array port '{port.Name}' without indexed connections.");
        }
    }

    public void InitializeArray<T>(IArrayInputPort<T> port, int count) {
        var targetGuid = mappings[(IPort) port];
        var sources = indexedInputs.TryGetValue((IPort) port, out var s) ? s : [];

        ((IArrayPortAssembly) port).AssembleWithCount(context, targetGuid, sources, count);
    }
}
