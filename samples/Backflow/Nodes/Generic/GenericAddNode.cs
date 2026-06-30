using System.Numerics;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes.Generic;

public class GenericAddNode<T> : AbstractGenericNode where T : struct, INumber<T> {
    public IInputPort<T> Number1 { get; }
    public IInputPort<T> Number2 { get; }
    public IOutputPort<T> Sum { get; }

    public GenericAddNode() {
        Number1 = Input(
            new NumericPortBuilder<T>(nameof(Number1))
                .Input()
        );
        Number2 = Input(
            new NumericPortBuilder<T>(nameof(Number2))
                .Input()
        );
        Sum = Output(
            new NumericPortBuilder<T>(nameof(Sum))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Sum.Write(context, Number1.Read(context) + Number2.Read(context));
        return ValueTask.FromResult(true);
    }
}
