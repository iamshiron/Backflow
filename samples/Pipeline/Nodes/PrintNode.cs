using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class PrintNode : AbstractNode {
    public Port Message { get; }

    public PrintNode() {
        Message = Input(nameof(Message));
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Console.WriteLine($"Message: {Message.Read<int>(context)}");
        return ValueTask.FromResult(true);
    }
}
