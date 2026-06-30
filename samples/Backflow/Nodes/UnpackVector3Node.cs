using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Backflow.Samples.Nodes;

public class UnpackVector3Node : AbstractNode {
    public IInputPort<Vector3D<int>> In { get; }
    public IOutputPort<int> X { get; }
    public IOutputPort<int> Y { get; }
    public IOutputPort<int> Z { get; }

    public UnpackVector3Node() {
        In = Input(
            new Vector3PortBuilder<int>(nameof(In))
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

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var vector = In.Read(context);
        X.Write(context, vector.X);
        Y.Write(context, vector.Y);
        Z.Write(context, vector.Z);
        return ValueTask.FromResult(true);
    }
}
