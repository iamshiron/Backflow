using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Types;

namespace Shiron.Backflow.Samples.Nodes;

public class BufferizeNode : AbstractNode {
    public IInputPort<IBlob> In { get; }
    public IOutputPort<IBlob> Out { get; }

    public BufferizeNode() {
        In = Input(
            new BlobPortBuilder<IBlob>(nameof(In))
                .Input()
        );
        Out = Output(
            new BlobPortBuilder<IBlob>(nameof(Out))
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var blob = In.Read(context)!;

        using var stream = blob.Storage.OpenRead();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        Out.Write(context, new RawBlob(new BufferData(ms.ToArray())));
        return true;
    }
}
