using Shiron.Backflow.Context;
using Shiron.Backflow.Port;

namespace Shiron.Backflow.Node;

/// <summary>
/// Base class for pipeline nodes. Declare ports in the constructor via <see cref="Input"/> and <see cref="Output"/>.
/// </summary>
/// <remarks>
/// Definitions are shared across graph occurrences and concurrent executions. Keep definitions and behaviors
/// stateless after construction; read execution-dependent values from the supplied context. Registration or
/// adding a node to a builder freezes its ports, behaviors and cache policy.
/// </remarks>
public abstract class AbstractNode {
    private readonly List<Port.Port> _inputs = [];
    private readonly List<Port.Port> _outputs = [];
    private IReadOnlyList<Port.Port>? _ports;
    private bool _frozen;
    private bool _useCache;

    internal void Freeze() {
        if (_frozen) return;
        _ports = Array.AsReadOnly(_inputs.Concat(_outputs).ToArray());
        foreach (var port in _ports) port.Freeze();
        _frozen = true;
    }

    private void EnsureMutable() {
        if (_frozen) throw new InvalidOperationException("Node definitions cannot change after registration or compilation.");
    }
    private readonly List<INodeBehavior> _behaviors = [];
    private INodeBehavior[] _behaviorSnapshot = [];

    /// <summary>
    /// Implement the node's core logic. Return <c>true</c> on success, <c>false</c> on failure.
    /// Read from input ports and write to output ports via <paramref name="context"/>.
    /// </summary>
    protected abstract ValueTask<bool> ExecuteNodeAsync(INodeContext context);

    /// <summary>
    /// Run the full execution lifecycle: pre-execute behaviors, core logic, post-execute behaviors.
    /// Returns the resulting <see cref="NodeState"/>.
    /// </summary>
    public async ValueTask<NodeState> ExecuteAsync(INodeContext context) {
        var executeNode = true;
        for (var i = 0; i < _behaviorSnapshot.Length; ++i) {
            var (shouldContinue, bResult) = await _behaviorSnapshot[i].PreExecuteAsync(context);
            if (!bResult) return NodeState.Failed;
            if (!shouldContinue) executeNode = false;
        }

        var coreResult = true;
        if (executeNode) {
            coreResult = await ExecuteNodeAsync(context);
        }

        var finalSuccessState = executeNode && coreResult;
        for (var i = 0; i < _behaviorSnapshot.Length; ++i) {
            await _behaviorSnapshot[i].PostExecuteAsync(context, finalSuccessState);
        }

        return executeNode switch {
            true when coreResult => NodeState.Done,
            true => NodeState.Failed,
            false => NodeState.Skipped
        };
    }

    /// <summary>Attach a lifecycle behavior. <see cref="INodeBehavior.AttachPorts"/> is called immediately.</summary>
    protected void AddBehavior(INodeBehavior behavior) {
        EnsureMutable();
        _behaviors.Add(behavior);
        behavior.AttachPorts(this);
        _behaviorSnapshot = _behaviors.ToArray();
    }

    /// <summary>All ports (inputs + outputs) on this node.</summary>
    public IReadOnlyList<Port.Port> Ports => _ports ?? Array.AsReadOnly(_inputs.Concat(_outputs).ToArray());
    /// <summary>Input ports on this node.</summary>
    public IReadOnlyList<Port.Port> Inputs => _inputView ??= _inputs.AsReadOnly();
    private IReadOnlyList<Port.Port>? _inputView;
    /// <summary>Output ports on this node.</summary>
    public IReadOnlyList<Port.Port> Outputs => _outputView ??= _outputs.AsReadOnly();
    private IReadOnlyList<Port.Port>? _outputView;

    /// <summary>When <c>true</c>, the <see cref="PipelineExecutor"/> will cache this node's outputs.</summary>
    public bool UseCache {
        get => _useCache;
        protected set { EnsureMutable(); _useCache = value; }
    }

    /// <summary>Register and return a typed input port on this node.</summary>
    public IInputPort<T> Input<T>(IInputPort<T> port) {
        EnsureMutable();
        _inputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    /// <summary>Register and return a typed array input port on this node.</summary>
    public IArrayInputPort<T> Input<T>(IArrayInputPort<T> port) {
        EnsureMutable();
        _inputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    /// <summary>Register and return a typed output port on this node.</summary>
    public IOutputPort<T> Output<T>(IOutputPort<T> port) {
        EnsureMutable();
        _outputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    /// <summary>Register and return a typed array output port on this node.</summary>
    public IArrayOutputPort<T> Output<T>(IArrayOutputPort<T> port) {
        EnsureMutable();
        _outputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
}
