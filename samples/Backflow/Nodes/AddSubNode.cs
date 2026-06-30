using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

public class AddSubNode : AbstractNode {
    public IInputPort<int> Number1 { get; }
    public IInputPort<int> Number2 { get; }
    public IInputPort<bool> IsSubtract { get; }

    public IOutputPort<int> Result { get; }

    public AddSubNode() {
        Number1 = Input(
            new NumericPortBuilder<int>(nameof(Number1))
                .Input()
        );
        Number2 = Input(
            new NumericPortBuilder<int>(nameof(Number2))
                .Input()
        );
        IsSubtract = Input(
            new BoolPortBuilder(nameof(IsSubtract))
                .Input()
        );

        Result = Output(
            new NumericPortBuilder<int>(nameof(Result))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var isSubstract = IsSubtract.Read(context);
        Console.WriteLine($"{nameof(IsSubtract)}: {isSubstract}");

        if (IsSubtract.Read(context)) {
            Result.Write(context, Number1.Read(context) - Number2.Read(context));
        } else {
            Result.Write(context, Number1.Read(context) + Number2.Read(context));
        }

        return ValueTask.FromResult(true);
    }
}
