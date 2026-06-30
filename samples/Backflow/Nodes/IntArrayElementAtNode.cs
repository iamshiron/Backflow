using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Samples.Nodes;

public class IntArrayElementAtNode : AbstractNode {
    public IInputPort<int[]> Array { get; }
    public IInputPort<int> Index { get; }
    public IOutputPort<int> Out { get; }

    public IntArrayElementAtNode() {
        Array = Input(
            new ArrayPortBuilder<int>(nameof(Array))
                .Input()
        );
        Index = Input(
            new NumericPortBuilder<int>(nameof(Index))
                .Input()
        );
        Out = Output(
            new NumericPortBuilder<int>(nameof(Out))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var array = Array.Read(context)!;
        var index = Index.Read(context);
        if (index < 0 || index >= array.Length) {
            Console.WriteLine($"Array index out of range {index} for array of length {array.Length}");
            return ValueTask.FromResult(false);
        }

        Out.Write(context, array[index]);
        return ValueTask.FromResult(true);
    }
}
