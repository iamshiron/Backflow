using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline;

public abstract class AbstractNode {
    public readonly List<Port> Inputs = [];
    public readonly List<Port> Outputs = [];
    public IEnumerable<Port> Ports => Inputs.Concat(Outputs);

    public abstract ValueTask<bool> Execute(INodeContext context);

    protected Port Input(string name) {
        var port = new Port(name);
        Inputs.Add(port);
        return port;
    }
    protected Port Output(string name) {
        var port = new Port(name);
        Outputs.Add(port);
        return port;
    }
}
