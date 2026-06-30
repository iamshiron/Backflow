using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

public class DoubleMultiplierNode : AbstractNode {
    public IInputPort<double> Value { get; }
    public IInputPort<double> Factor { get; }
    public IOutputPort<double> Result { get; }

    public DoubleMultiplierNode() {
        Value = Input(
            new NumericPortBuilder<double>(nameof(Value))
                .Input()
        );
        Factor = Input(
            new NumericPortBuilder<double>(nameof(Factor))
                .Input()
        );
        Result = Output(
            new NumericPortBuilder<double>(nameof(Result))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Result.Write(context, Value.Read(context) * Factor.Read(context));
        return ValueTask.FromResult(true);
    }
}
