using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Benchmarks.Pipeline;

internal sealed class PassThroughNode : AbstractNode {
    public IInputPort<int> Input { get; }
    public IOutputPort<int> Output { get; }

    public PassThroughNode() {
        Input = Input(
            new NumericPortBuilder<int>(nameof(Input)).Input()
        );
        Output = Output(
            new NumericPortBuilder<int>(nameof(Output)).Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Output.Write(context, Input.Read(context));
        return ValueTask.FromResult(true);
    }
}

internal sealed class AddIntNode : AbstractNode {
    public IInputPort<int> Number1 { get; }
    public IInputPort<int> Number2 { get; }
    public IOutputPort<int> Sum { get; }

    public AddIntNode() {
        Number1 = Input(
            new NumericPortBuilder<int>(nameof(Number1)).Input()
        );
        Number2 = Input(
            new NumericPortBuilder<int>(nameof(Number2)).Input()
        );
        Sum = Output(
            new NumericPortBuilder<int>(nameof(Sum)).Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Sum.Write(context, Number1.Read(context) + Number2.Read(context));
        return ValueTask.FromResult(true);
    }
}

internal sealed class MultiplyIntNode : AbstractNode {
    public IInputPort<int> Number { get; }
    public IInputPort<int> Factor { get; }
    public IOutputPort<int> Result { get; }

    public MultiplyIntNode() {
        Number = Input(
            new NumericPortBuilder<int>(nameof(Number)).Input()
        );
        Factor = Input(
            new NumericPortBuilder<int>(nameof(Factor)).Input()
        );
        Result = Output(
            new NumericPortBuilder<int>(nameof(Result)).Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Result.Write(context, Number.Read(context) * Factor.Read(context));
        return ValueTask.FromResult(true);
    }
}

internal sealed class IntAccumulateNode : AbstractNode {
    public IArrayInputPort<int> Values { get; }
    public IOutputPort<int> Total { get; }

    public IntAccumulateNode() {
        Values = Input(
            new ArrayPortBuilder<int>(nameof(Values)).Input()
        );
        Total = Output(
            new NumericPortBuilder<int>(nameof(Total)).Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var values = Values.Read(context);
        var sum = 0;
        if (values is not null) {
            for (var i = 0; i < values.Length; i++)
                sum += values[i];
        }
        Total.Write(context, sum);
        return ValueTask.FromResult(true);
    }
}

internal static class BenchmarkRegistry {
    public static NodeRegistry Create() {
        var registry = new NodeRegistry();
        registry.Register<PassThroughNode>();
        registry.Register<AddIntNode>();
        registry.Register<MultiplyIntNode>();
        registry.Register<IntAccumulateNode>();
        return registry;
    }
}
