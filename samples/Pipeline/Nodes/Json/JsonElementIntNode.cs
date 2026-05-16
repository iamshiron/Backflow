using System.Text.Json;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes.Json;

public class JsonElementIntNode : AbstractNode {
    public IInputPort<JsonElement> Element { get; }
    public IOutputPort<int> Out { get; }

    public JsonElementIntNode() {
        Element = Input(
            new JsonElementPortBuilder(nameof(Element))
                .Input()
        );
        Out = Output(
            new NumericPortBuilder<int>(nameof(Out))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var element = Element.Read(context)!;
        Out.Write(context, element.GetInt32());
        return ValueTask.FromResult(true);
    }
}
