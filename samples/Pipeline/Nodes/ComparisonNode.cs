using System.Numerics;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class ComparisonNode : AbstractNode {
    public IInputPort<int> A { get; }
    public IInputPort<int> B { get; }
    public IInputPort<ComparisonOperator> Operator { get; }
    public IOutputPort<bool> Result { get; }

    public ComparisonNode() {
        A = Input(
            new NumericPortBuilder<int>(nameof(A))
                .Input()
        );
        B = Input(
            new NumericPortBuilder<int>(nameof(B))
                .Input()
        );
        Operator = Input(
            new EnumPortBuilder<ComparisonOperator>(nameof(Operator))
                .Input()
        );
        Result = Output(
            new BoolPortBuilder(nameof(Result))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var v1 = A.Read(context);
        var v2 = B.Read(context);
        var op = Operator.Read(context);

        switch (op) {
            case ComparisonOperator.Equal:
                Result.Write(context, EqualityComparer<int>.Default.Equals(v1, v2));
                break;
            case ComparisonOperator.NotEqual:
                Result.Write(context, !EqualityComparer<int>.Default.Equals(v1, v2));
                break;
            case ComparisonOperator.GreaterThan:
                Result.Write(context, Comparer<int>.Default.Compare(v1, v2) > 0);
                break;
            case ComparisonOperator.LessThan:
                Result.Write(context, Comparer<int>.Default.Compare(v1, v2) < 0);
                break;
            case ComparisonOperator.GreaterThanOrEqual:
                Result.Write(context, Comparer<int>.Default.Compare(v1, v2) >= 0);
                break;
            case ComparisonOperator.LessThanOrEqual:
                Result.Write(context, Comparer<int>.Default.Compare(v1, v2) <= 0);
                break;

            default:
                return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(true);
    }
}

public enum ComparisonOperator {
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}
