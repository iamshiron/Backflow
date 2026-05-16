using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

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
