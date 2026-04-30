using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Numeric;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class SubtractNode : AbstractNode {
    public IInputPort<int> Number1 { get; }
    public IInputPort<int> Number2 { get; }
    public IOutputPort<int> Diff { get; }

    public SubtractNode() {
        Number1 = Input(
            new NumericPortBuilder<int>(nameof(Number1))
                .Input()
        );
        Number2 = Input(
            new NumericPortBuilder<int>(nameof(Number2))
                .Input()
        );
        Diff = Output(
            new NumericPortBuilder<int>(nameof(Diff))
                .Output()
        );
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Diff.Write(context, Number1.Read(context) - Number2.Read(context));
        return ValueTask.FromResult(true);
    }
}
