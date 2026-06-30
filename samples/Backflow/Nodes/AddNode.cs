using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Node.Behvaior;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

public class AddNode : AbstractNode {
    public IInputPort<int> Number1 { get; }
    public IInputPort<int> Number2 { get; }
    public IOutputPort<int> Sum { get; }
    public ChipEnableBehavior ChipEnableBehavior { get; } = new();
    public EnableOutBehavior EnableOutBehavior { get; } = new();

    public AddNode() {
        AddBehavior(ChipEnableBehavior);
        AddBehavior(EnableOutBehavior);

        Number1 = Input(
            new NumericPortBuilder<int>(nameof(Number1))
                .Input()
        );
        Number2 = Input(
            new NumericPortBuilder<int>(nameof(Number2))
                .Input()
        );
        Sum = Output(
            new NumericPortBuilder<int>(nameof(Sum))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Sum.Write(context, Number1.Read(context) + Number2.Read(context));
        return ValueTask.FromResult(true);
    }
}
