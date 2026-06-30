using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Backflow.Samples.Nodes;

public class PackVector3Node : AbstractNode {
    public IInputPort<int> X { get; }
    public IInputPort<int> Y { get; }
    public IInputPort<int> Z { get; }
    public IOutputPort<Vector3D<int>> Out { get; }

    public PackVector3Node() {
        X = Input(
            new NumericPortBuilder<int>(nameof(X))
                .Input()
        );
        Y = Input(
            new NumericPortBuilder<int>(nameof(Y))
                .Input()
        );
        Z = Input(
            new NumericPortBuilder<int>(nameof(Z))
                .Input()
        );

        Out = Output(
            new Vector3PortBuilder<int>(nameof(Out))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Out.Write(context, new Vector3D<int>(X.Read(context), Y.Read(context), Z.Read(context)));
        return ValueTask.FromResult(true);
    }
}
