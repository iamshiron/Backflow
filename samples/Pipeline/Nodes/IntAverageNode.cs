using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class IntAverageNode : AbstractNode {
    public IInputPortGroup<int> Values { get; }
    public IOutputPort<double> Average { get; }

    public IntAverageNode() {
        Values = InputGroup(
            new PortGroupBuilder<int>(nameof(Values))
                .Using(new NumericPortBuilder<int>(""))
                .MinCount(1)
                .Input()
        );
        Average = Output(new OutputPort<double>(nameof(Average)));
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var values = Values.ReadAll(context);
        Average.Write(context, values.Count > 0 ? values.Average() : 0.0);
        return ValueTask.FromResult(true);
    }
}
