using Shiron.Backflow.Node;

namespace Shiron.Backflow.Context;

/// <summary>Context-owned lifecycle state. A context can be reused sequentially, never concurrently.</summary>
public sealed class PipelineExecutionState {
    private object? _plan;
    private sealed class Storage(PipelineBuilder.NodeInstance[] nodes) {
        internal readonly Dictionary<string, int> Indices = nodes.Select((node, index) => (node.ID, index))
            .ToDictionary(pair => pair.ID, pair => pair.index);
        internal readonly int[] States = new int[nodes.Length];
        internal readonly NodeContext?[] Contexts = new NodeContext?[nodes.Length];
    }

    private Storage _storage = new([]);
    private int _active;
    internal bool HasRun { get; private set; }

    /// <summary>Gets this context's most recent state for a graph node.</summary>
    public NodeState GetNodeState(PipelineBuilder.NodeInstance node) {
        var storage = Volatile.Read(ref _storage);
        return storage.Indices.TryGetValue(node.ID, out var index)
            ? (NodeState) Volatile.Read(ref storage.States[index]) : NodeState.Pending;
    }

    internal void Begin(object plan, PipelineBuilder.NodeInstance[] nodes) {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new InvalidOperationException("A pipeline context cannot execute concurrently. Use a separate context per execution.");
        try {
            if (!ReferenceEquals(_plan, plan)) {
                Volatile.Write(ref _storage, new Storage(nodes));
                _plan = plan;
            }
            Array.Fill(_storage.States, (int) NodeState.Pending);
        } catch {
            End();
            throw;
        }
    }

    internal NodeState Get(int index) => (NodeState) Volatile.Read(ref _storage.States[index]);
    internal NodeContext? GetContext(int index) => _storage.Contexts[index];
    internal void SetContext(int index, NodeContext context) => _storage.Contexts[index] = context;
    internal void Set(int index, NodeState state) => Volatile.Write(ref _storage.States[index], (int) state);
    internal void End() {
        HasRun = true;
        Volatile.Write(ref _active, 0);
    }
}
