using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Node;

/// <summary>
/// Base class for pipeline nodes. Declare ports in the constructor via <see cref="Input"/> and <see cref="Output"/>.
/// </summary>
public abstract class AbstractNode {
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
        _behaviors.Add(behavior);
        behavior.AttachPorts(this);
        _behaviorSnapshot = _behaviors.ToArray();
    }

    /// <summary>All ports (inputs + outputs) on this node.</summary>
    public List<Port.Port> Ports => Inputs.Concat(Outputs).ToList();
    /// <summary>Input ports on this node.</summary>
    public List<Port.Port> Inputs { get; } = [];
    /// <summary>Output ports on this node.</summary>
    public List<Port.Port> Outputs { get; } = [];

    /// <summary>When <c>true</c>, the <see cref="PipelineExecutor"/> will cache this node's outputs.</summary>
    public bool UseCache { get; protected set; }

    /// <summary>Register and return a typed input port on this node.</summary>
    public IInputPort<T> Input<T>(IInputPort<T> port) {
        Inputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    /// <summary>Register and return a typed array input port on this node.</summary>
    public IArrayInputPort<T> Input<T>(IArrayInputPort<T> port) {
        Inputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    /// <summary>Register and return a typed output port on this node.</summary>
    public IOutputPort<T> Output<T>(IOutputPort<T> port) {
        Outputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    /// <summary>Register and return a typed array output port on this node.</summary>
    public IArrayOutputPort<T> Output<T>(IArrayOutputPort<T> port) {
        Outputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
}
