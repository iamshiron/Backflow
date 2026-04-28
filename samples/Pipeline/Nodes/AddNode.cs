using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class AddNode : AbstractNode {
    public Port Number1 { get; }
    public Port Number2 { get; }
    public Port Sum { get; }

    public AddNode() {
        Number1 = Input();
        Number2 = Input();
        Sum = Output();
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Sum.Write(context, Number1.Read<int>(context) + Number2.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}
