using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

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

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Diff.Write(context, Number1.Read(context) - Number2.Read(context));
        return ValueTask.FromResult(true);
    }
}
