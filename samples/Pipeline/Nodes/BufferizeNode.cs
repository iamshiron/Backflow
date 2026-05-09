using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

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
