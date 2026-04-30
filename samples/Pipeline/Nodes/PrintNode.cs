using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Numeric;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class PrintNode : AbstractNode {
    public IInputPort<object?> Message { get; }

    public PrintNode() {
        Message = Input(
            new AnyPortBuilder(nameof(Message))
                .Input()
        );
    }

    public override ValueTask<bool> Execute(INodeContext context) {
        Console.WriteLine($"Message: {Message.Read(context)}");
        return ValueTask.FromResult(true);
    }
}
