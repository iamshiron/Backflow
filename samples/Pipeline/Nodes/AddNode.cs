using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class AddNode : AbstractNode {
    public Port Number1 { get; }
    public Port Number2 { get; }
    public Port Sum { get; }

    public AddNode() {
        Number1 = Input(nameof(Number1));
        Number2 = Input(nameof(Number2));
        Sum = Output(nameof(Sum));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Sum.Write(context, Number1.Read<int>(context) + Number2.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}
