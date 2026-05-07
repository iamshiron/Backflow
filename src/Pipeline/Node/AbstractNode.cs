using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Node;

/// <summary>
/// Base class for pipeline nodes. Declare ports in the constructor via <see cref="Input"/> and <see cref="Output"/>.
/// </summary>
public abstract class AbstractNode {
    private readonly List<INodeBehavior> _behaviors = [];
    private INodeBehavior[] _behaviorSnapshot = [];

    protected abstract ValueTask<bool> ExecuteNodeAsync(INodeContext context);

    public async ValueTask<NodeState> ExecuteAsync(INodeContext context) {
        var executeNode = true;
        for (var i = 0; i < _behaviorSnapshot.Length; ++i) {
            var (shouldContinue, bResult) = await _behaviorSnapshot[i].PreExecuteAsync(context);
            if (!bResult) return NodeState.Failed;
            if (!shouldContinue) executeNode = false;
        }

        var coreResult = true;
        if (executeNode) {
            try {
                coreResult = await ExecuteNodeAsync(context);
            } catch {
                return NodeState.Failed;
            }
        }

        var finalSuccessState = executeNode && coreResult;
        for (var i = 0; i < _behaviorSnapshot.Length; ++i) {
            await _behaviorSnapshot[i].PostExecuteAsync(context, finalSuccessState);
        }

        return executeNode switch {
            true when coreResult => NodeState.Done,
            true => NodeState.Failed,
            false => NodeState.Skipped,
        };
    }

    protected void AddBehavior(INodeBehavior behavior) {
        _behaviors.Add(behavior);
        behavior.AttachPorts(this);
        _behaviorSnapshot = _behaviors.ToArray();
    }

    public List<Port.Port> Ports => Inputs.Concat(Outputs).ToList();
    public List<Port.Port> Inputs { get; } = [];
    public List<Port.Port> Outputs { get; } = [];

    public bool UseCache { get; protected set; } = true;

    public IInputPort<T> Input<T>(IInputPort<T> port) {
        Inputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    public IOutputPort<T> Output<T>(IOutputPort<T> port) {
        Outputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
}
