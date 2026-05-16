using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class ConcatNode : AbstractNode {
    public IInputPort<string> String1 { get; }
    public IInputPort<string> String2 { get; }
    public IOutputPort<string> Concatenated { get; }

    public ConcatNode() {
        String1 = Input(
            new StringPortBuilder(nameof(String1))
                .Default(string.Empty)
                .Input()
        );
        String2 = Input(
            new StringPortBuilder(nameof(String2))
                .Default(string.Empty)
                .Input()
        );

        Concatenated = Output(
            new StringPortBuilder(nameof(Concatenated)).Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Concatenated.Write(context, String1.Read(context) + String2.Read(context));
        return ValueTask.FromResult(true);
    }
}
