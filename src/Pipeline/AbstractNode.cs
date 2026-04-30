using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Base class for pipeline nodes. Declare ports in the constructor via <see cref="Input"/> and <see cref="Output"/>.
/// </summary>
public abstract class AbstractNode {
    /// <summary>Input ports declared by this node.</summary>
    public readonly List<Port.Port> Inputs = [];

    /// <summary>Output ports declared by this node.</summary>
    public readonly List<Port.Port> Outputs = [];

    /// <summary>All ports (inputs + outputs).</summary>
    public IEnumerable<Port.Port> Ports => Inputs.Concat(Outputs);

    /// <summary>
    /// Execute the node's logic. Read from input ports, write to output ports via <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Per-node context for reading/writing port data.</param>
    /// <returns><c>true</c> on success, <c>false</c> to signal failure.</returns>
    public abstract ValueTask<bool> Execute(INodeContext context);

    /// <summary>Declare an input port. Call in the constructor.</summary>
    /// <param name="name">Unique name for this port within the node.</param>
    protected Port.Port Input(string name) {
        var port = new Port.Port(name);
        Inputs.Add(port);
        return port;
    }

    /// <summary>Declare an output port. Call in the constructor.</summary>
    /// <param name="name">Unique name for this port within the node.</param>
    protected Port.Port Output(string name) {
        var port = new Port.Port(name);
        Outputs.Add(port);
        return port;
    }
}
