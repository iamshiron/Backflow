using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

public class IntRangeArrayNode : AbstractNode {
    public IInputPort<int> Size { get; }
    public IOutputPort<int[]> Out { get; }

    public IntRangeArrayNode() {
        Size = Input(
            new NumericPortBuilder<int>(nameof(Size))
                .Input()
        );
        Out = Output(
            new ArrayPortBuilder<int>(nameof(Out))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var size = Size.Read(context);
        var result = Enumerable.Range(0, size).ToArray();
        Out.Write(context, result);
        return ValueTask.FromResult(true);
    }
}
