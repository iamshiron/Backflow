using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class IntArrayLengthNode : AbstractNode {
    public IInputPort<int[]> Array { get; }
    public IOutputPort<int> Length { get; }

    public IntArrayLengthNode() {
        Array = Input(
            new ArrayPortBuilder<int>(nameof(Array))
                .Input()
        );
        Length = Output(
            new NumericPortBuilder<int>(nameof(Length))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Length.Write(context, Array.Read(context)!.Length);
        return ValueTask.FromResult(true);
    }
}
