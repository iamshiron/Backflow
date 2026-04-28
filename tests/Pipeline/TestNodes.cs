using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Tests.Pipeline;

public class AddNode : AbstractNode {
    public Port Number1 { get; }
    public Port Number2 { get; }
    public Port Sum { get; }

    public AddNode() {
        Number1 = Input(nameof(Number1));
        Number2 = Input(nameof(Number2));
        Sum = Output(nameof(Sum));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Sum.Write(context, Number1.Read<int>(context) + Number2.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}

public class SubtractNode : AbstractNode {
    public Port Number1 { get; }
    public Port Number2 { get; }
    public Port Diff { get; }

    public SubtractNode() {
        Number1 = Input(nameof(Number1));
        Number2 = Input(nameof(Number2));
        Diff = Output(nameof(Diff));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Diff.Write(context, Number1.Read<int>(context) - Number2.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}

public class MultiplyNode : AbstractNode {
    public Port Number1 { get; }
    public Port Number2 { get; }
    public Port Product { get; }

    public MultiplyNode() {
        Number1 = Input(nameof(Number1));
        Number2 = Input(nameof(Number2));
        Product = Output(nameof(Product));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Product.Write(context, Number1.Read<int>(context) * Number2.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}

public class PassThroughNode : AbstractNode {
    public Port In { get; }
    public Port Out { get; }

    public PassThroughNode() {
        In = Input(nameof(In));
        Out = Output(nameof(Out));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Out.Write(context, In.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}

public class CollectorNode : AbstractNode {
    public Port Value { get; }
    public new Port Output { get; }

    public int ExecutionCount { get; private set; }
    public List<object> CollectedValues { get; } = [];

    public CollectorNode() {
        Value = Input(nameof(Value));
        Output = Output(nameof(Output));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        ExecutionCount++;
        var val = Value.Read<int>(context);
        CollectedValues.Add(val);
        Output.Write(context, val);
        return ValueTask.FromResult(true);
    }
}

public class NoInputNode : AbstractNode {
    public Port Result { get; }
    public bool WasExecuted { get; private set; }

    public NoInputNode() {
        Result = Output(nameof(Result));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        WasExecuted = true;
        Result.Write(context, 42);
        return ValueTask.FromResult(true);
    }
}

public class PrintNode : AbstractNode {
    public Port Message { get; }

    public PrintNode() {
        Message = Input(nameof(Message));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        var val = Message.Read<int>(context);
        return ValueTask.FromResult(true);
    }
}
