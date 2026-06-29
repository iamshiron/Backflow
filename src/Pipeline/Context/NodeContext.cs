using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Default <see cref="INodeContext"/> implementation. Delegates reads/writes to the
/// global <see cref="IPipelineContext"/> via the node's port-to-channel mapping.
/// </summary>
public sealed class NodeContext(
    IPipelineContext context,
    IReadOnlyDictionary<IPort, int> mappings,
    IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>> indexedInputs
) : INodeContext {
    public NodeContext(IPipelineContext context, IReadOnlyDictionary<IPort, int> mappings)
        : this(context, mappings, new Dictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>()) {
    }

    public void Write<T>(IPort port, T? value) {
        context.Write(mappings[port], value);
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

    /// <summary>Initialize an array port from its indexed connections (count inferred from max index).</summary>
    public void InitializeArray<T>(IArrayInputPort<T> port) {
        var targetChannel = mappings[(IPort) port];

        if (indexedInputs.TryGetValue((IPort) port, out var sources) && sources.Count > 0) {
            var count = sources.Max(s => s.Index) + 1;
            ((IArrayPortAssembly) port).AssembleWithCount(context, targetChannel, sources, count);
        } else {
            throw new InvalidOperationException(
                $"Cannot initialize array port '{port.Name}' without indexed connections.");
        }
    }

    public void InitializeArray<T>(IArrayInputPort<T> port, int count) {
        var targetChannel = mappings[(IPort) port];
        var sources = indexedInputs.TryGetValue((IPort) port, out var s) ? s : [];

        ((IArrayPortAssembly) port).AssembleWithCount(context, targetChannel, sources, count);
    }
}
