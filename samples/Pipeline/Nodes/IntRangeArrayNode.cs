using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

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
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var size = Size.Read(context);
        var result = Enumerable.Range(0, size).ToArray();
        Out.Write(context, result);
        return ValueTask.FromResult(true);
    }
}
