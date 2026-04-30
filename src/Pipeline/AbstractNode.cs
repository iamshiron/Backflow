using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Base class for pipeline nodes. Declare ports in the constructor via <see cref="Input"/> and <see cref="Output"/>.
/// </summary>
public abstract class AbstractNode {
    /// <summary>
    /// Execute the node's logic. Read from input ports, write to output ports via <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Per-node context for reading/writing port data.</param>
    /// <returns><c>true</c> on success, <c>false</c> to signal failure.</returns>
    public abstract ValueTask<bool> Execute(INodeContext context);
    public List<Port.Port> Ports => Inputs.Concat(Outputs).ToList();
    public List<Port.Port> Inputs { get; } = [];
    public List<Port.Port> Outputs { get; } = [];

    protected IInputPort<T> Input<T>(IInputPort<T> port) {
        Inputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
    protected IOutputPort<T> Output<T>(IOutputPort<T> port) {
        Outputs.Add(port as Port.Port ?? throw new ArgumentException("Port must be an instance of Port<T>", nameof(port)));
        return port;
    }
}
