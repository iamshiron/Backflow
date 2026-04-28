using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class SubtractNode : AbstractNode {
    public Port Number1 { get; }
    public Port Number2 { get; }
    public Port Diff { get; }

    public SubtractNode() {
        Number1 = Input(nameof(Number1));
        Number2 = Input(nameof(Number2));
        Diff = Output(nameof(Diff));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Diff.Write(context, Number1.Read<int>(context) - Number2.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}
