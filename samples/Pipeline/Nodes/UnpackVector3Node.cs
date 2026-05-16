using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

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
