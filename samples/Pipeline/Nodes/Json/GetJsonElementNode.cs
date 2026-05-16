using System.Text.Json;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes.Json;

public class GetJsonElementNode : AbstractNode {
    public IInputPort<string> Path { get; }
    public IInputPort<JsonDocument> Json { get; }
    public IOutputPort<JsonElement> Element { get; }

    public GetJsonElementNode() {
        Path = Input(
            new StringPortBuilder(nameof(Path))
                .Input()
        );
        Json = Input(
            new JsonDocumentPortBuilder(nameof(Json))
                .Input()
        );
        Element = Output(
            new JsonElementPortBuilder(nameof(Element))
                .Output()
        );

        UseCache = true;
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var path = Path.Read(context)!;
        var document = Json.Read(context)!;

        Element.Write(context, document.RootElement.GetProperty(path));
        return ValueTask.FromResult(true);
    }
}
