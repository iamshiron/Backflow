using System.Collections;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Default <see cref="INodeContext"/> implementation. Delegates reads/writes to the
/// global <see cref="IPipelineContext"/> via the node's port-to-channel mapping.
/// </summary>
public sealed class NodeContext(
    IPipelineContext context,
    IReadOnlyDictionary<IPort, int> mappings,
    IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>> indexedInputs,
    Dictionary<IPort, BitArray>? suppliedMasks = null
) : INodeContext {
    private readonly Dictionary<IPort, BitArray> _suppliedMasks = suppliedMasks ?? new Dictionary<IPort, BitArray>();

    public NodeContext(IPipelineContext context, IReadOnlyDictionary<IPort, int> mappings)
        : this(context, mappings, new Dictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>(), null) {
    }

    public NodeContext(IPipelineContext context, IReadOnlyDictionary<IPort, int> mappings,
                       IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>> indexedInputs)
        : this(context, mappings, indexedInputs, null) {
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

    /// <inheritdoc/>
    public bool IsSlotSupplied(IPort port, int index) {
        if (_suppliedMasks.TryGetValue(port, out var mask))
            return index >= 0 && index < mask.Length && mask[index];
        return context.HasAny(mappings[port]);
    }

    /// <summary>Initialize an array port from its indexed connections (count inferred from max index).</summary>
    public void InitializeArray<T>(IArrayInputPort<T> port) {
        var targetChannel = mappings[(IPort) port];

        if (indexedInputs.TryGetValue((IPort) port, out var sources) && sources.Count > 0) {
            var count = sources.Max(s => s.Index) + 1;
            var mask = ((IArrayPortAssembly) port).Assemble(context, targetChannel, sources, count);
            _suppliedMasks[(IPort) port] = mask;
        } else {
            throw new InvalidOperationException(
                $"Cannot initialize array port '{port.Name}' without indexed connections.");
        }
    }

    public void InitializeArray<T>(IArrayInputPort<T> port, int count) {
        var targetChannel = mappings[(IPort) port];
        var sources = indexedInputs.TryGetValue((IPort) port, out var s) ? s : [];

        var mask = ((IArrayPortAssembly) port).Assemble(context, targetChannel, sources, count);
        _suppliedMasks[(IPort) port] = mask;
    }
}
