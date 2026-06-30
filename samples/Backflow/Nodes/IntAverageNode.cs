using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

public class IntAverageNode : AbstractNode {
    public IArrayInputPort<int> Values { get; }
    public IOutputPort<double> Average { get; }

    public IntAverageNode() {
        Values = Input(
            new ArrayPortBuilder<int>(nameof(Values))
                .Using(new NumericPortBuilder<int>(""))
                .MinCount(1)
                .Input()
        );
        Average = Output(new OutputPort<double>(nameof(Average)));

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var values = Values.Read(context);
        Average.Write(context, values is { Length: > 0 } ? values.Average() : 0.0);
        return ValueTask.FromResult(true);
    }
}
