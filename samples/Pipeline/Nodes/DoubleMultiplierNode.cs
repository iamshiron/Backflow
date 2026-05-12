using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

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
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Result.Write(context, Value.Read(context) * Factor.Read(context));
        return ValueTask.FromResult(true);
    }
}
