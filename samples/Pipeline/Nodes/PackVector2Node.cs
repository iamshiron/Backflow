using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class PackVector2Node : AbstractNode {
    public IInputPort<int> X { get; }
    public IInputPort<int> Y { get; }
    public IOutputPort<Vector2D<int>> Out { get; }

    public PackVector2Node() {
        X = Input(
            new NumericPortBuilder<int>(nameof(X))
                .Input()
        );
        Y = Input(
            new NumericPortBuilder<int>(nameof(Y))
                .Input()
        );

        Out = Output(
            new Vector2PortBuilder<int>(nameof(Out))
                .Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Out.Write(context, new Vector2D<int>(X.Read(context), Y.Read(context)));
        return ValueTask.FromResult(true);
    }
}
