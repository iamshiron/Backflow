using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class PackVector4Node : AbstractNode {
    public IInputPort<int> X { get; }
    public IInputPort<int> Y { get; }
    public IInputPort<int> Z { get; }
    public IInputPort<int> W { get; }
    public IOutputPort<Vector4D<int>> Out { get; }

    public PackVector4Node() {
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
        W = Input(
            new NumericPortBuilder<int>(nameof(W))
                .Input()
        );

        Out = Output(
            new Vector4PortBuilder<int>(nameof(Out))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Out.Write(context, new Vector4D<int>(X.Read(context), Y.Read(context), Z.Read(context), W.Read(context)));
        return ValueTask.FromResult(true);
    }
}
