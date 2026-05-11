using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class PrintNode : AbstractNode {
    public IInputPort<object?> Message { get; }
    public IInputPort<string> Prefix { get; }

    public PrintNode() {
        Prefix = Input(
            new StringPortBuilder(nameof(Prefix))
                .Default("Message: ")
                .Input()
        );

        Message = Input(
            new AnyPortBuilder(nameof(Message))
                .Input()
        );

        UseCache = false;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var prefix = Prefix.Read(context);
        var data = Message.ReadAny(context);

        if (data is JsonDocument json) {
            Console.WriteLine($"{prefix}{json.RootElement.GetRawText()}");
            return ValueTask.FromResult(true);
        }

        Console.WriteLine($"{prefix}{data}");
        return ValueTask.FromResult(true);
    }
}
