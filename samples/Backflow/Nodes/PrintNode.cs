using System.Text.Json;
using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Services;

namespace Shiron.Backflow.Samples.Nodes;

public class PrintNode : AbstractNode {
    public IInputPort<object?> Message { get; }
    public IInputPort<string> Prefix { get; }

    private readonly IPrintService _printService;

    public PrintNode(IPrintService printService) {
        _printService = printService;

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
            _printService.Print($"{prefix}{json.RootElement.GetRawText()}");
            return ValueTask.FromResult(true);
        }

        _printService.Print($"{prefix}{data}");
        return ValueTask.FromResult(true);
    }
}
