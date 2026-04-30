using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Benchmarks.Pipeline;

public class IdentityNode : AbstractNode {
    public Port In { get; }
    public Port Out { get; }

    public IdentityNode() {
        In = Input(nameof(In));
        Out = Output(nameof(Out));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Out.Write(context, In.Read<int>(context));
        return ValueTask.FromResult(true);
    }
}

public class ComputeNode : AbstractNode {
    public Port InputA { get; }
    public Port InputB { get; }
    public Port Result { get; }

    public ComputeNode() {
        InputA = Input(nameof(InputA));
        InputB = Input(nameof(InputB));
        Result = Output(nameof(Result));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        var a = InputA.Read<int>(context);
        var b = InputB.Read<int>(context);
        Result.Write(context, a * b + a - b);
        return ValueTask.FromResult(true);
    }
}

public class MergeNode : AbstractNode {
    public Port InputA { get; }
    public Port InputB { get; }
    public Port InputC { get; }
    public new Port Output { get; }

    public MergeNode() {
        InputA = Input(nameof(InputA));
        InputB = Input(nameof(InputB));
        InputC = Input(nameof(InputC));
        Output = Output(nameof(Output));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        var a = InputA.Read<int>(context);
        var b = InputB.Read<int>(context);
        var c = InputC.Read<int>(context);
        Output.Write(context, a + b + c);
        return ValueTask.FromResult(true);
    }
}

public class MultiOutputNode : AbstractNode {
    public Port In { get; }
    public Port Out1 { get; }
    public Port Out2 { get; }
    public Port Out3 { get; }

    public MultiOutputNode() {
        In = Input(nameof(In));
        Out1 = Output(nameof(Out1));
        Out2 = Output(nameof(Out2));
        Out3 = Output(nameof(Out3));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        var val = In.Read<int>(context);
        Out1.Write(context, val);
        Out2.Write(context, val * 2);
        Out3.Write(context, val * 3);
        return ValueTask.FromResult(true);
    }
}
