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
    /// ExecuteNodeAsync the node's logic. Read from input ports, write to output ports via <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Per-node context for reading/writing port data.</param>
    /// <returns><c>true</c> on success, <c>false</c> to signal failure.</returns>
    protected abstract ValueTask<bool> ExecuteNodeAsync(INodeContext context);

    public async ValueTask<bool> ExecuteAsync(INodeContext context) {
        for (var i = 0; i < _behaviorSnapshot.Length; ++i) {
            var (shouldContinue, bResult) = await _behaviorSnapshot[i].PreExecuteAsync(context);
            if (!shouldContinue) return bResult;
        }

        var result = await ExecuteNodeAsync(context);

        for (var i = 0; i < _behaviorSnapshot.Length; ++i) {
            await _behaviorSnapshot[i].PostExecuteAsync(context, result);
        }

        return result;
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
