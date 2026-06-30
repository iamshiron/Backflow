using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Backflow.Samples.Nodes;

public class UnpackVector4Node : AbstractNode {
    public IInputPort<Vector4D<int>> In { get; }
    public IOutputPort<int> X { get; }
    public IOutputPort<int> Y { get; }
    public IOutputPort<int> Z { get; }
    public IOutputPort<int> W { get; }

    public UnpackVector4Node() {
        In = Input(
            new Vector4PortBuilder<int>(nameof(In))
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
        Z = Output(
            new NumericPortBuilder<int>(nameof(Z))
                .Output()
        );
        W = Output(
            new NumericPortBuilder<int>(nameof(W))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var vector = In.Read(context);
        X.Write(context, vector.X);
        Y.Write(context, vector.Y);
        Z.Write(context, vector.Z);
        W.Write(context, vector.W);
        return ValueTask.FromResult(true);
    }
}
