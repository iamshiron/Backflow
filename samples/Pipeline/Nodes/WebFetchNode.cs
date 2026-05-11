using System.Net.Http.Json;
using System.Text.Json;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class WebFetchNode : AbstractNode {
    private static readonly HttpClient Client = new();

    public IInputPort<string> Url { get; }
    public IInputPort<string> Method { get; }

    public IOutputPort<JsonDocument> Response { get; }
    public IOutputPort<int> ResponseCode { get; }

    public WebFetchNode() {
        Url = Input(
            new StringPortBuilder("Url")
                .Input()
        );
        Method = Input(
            new StringPortBuilder("Method")
                .Input()
        );
        Response = Output(
            new JsonPortBuilder("Response")
                .Output()
        );
        ResponseCode = Output(
            new NumericPortBuilder<int>("ResponseCode")
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            Url.Read(context)
        );
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadFromJsonAsync<JsonDocument>();
        ResponseCode.Write(context, (int) response.StatusCode);
        Response.Write(context, content ?? JsonDocument.Parse("{}"));

        return true;
    }
}
