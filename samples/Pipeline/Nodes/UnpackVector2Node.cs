using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class UnpackVector2Node : AbstractNode {
    public IInputPort<Vector2D<int>> In { get; }
    public IOutputPort<int> X { get; }
    public IOutputPort<int> Y { get; }

    public UnpackVector2Node() {
        In = Input(
            new Vector2PortBuilder<int>(nameof(In))
                .Input()
        );

        X = Output(
            new NumericPortBuilder<int>(nameof(X))
                .Output()
        );
        Y = Output(
            new NumericPortBuilder<int>(nameof(Y))
                .Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var vector = In.Read(context);
        X.Write(context, vector.X);
        Y.Write(context, vector.Y);
        return ValueTask.FromResult(true);
    }
}
