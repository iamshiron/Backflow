using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;
using Shiron.Lib.Samples.Pipeline.Types;
using Shiron.Lib.Samples.Pipeline.Types.Meta;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class BufferizeImageNode : AbstractNode {
    public IInputPort<IBlob<ImageMeta, IStreamData>> In { get; }
    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

    public BufferizeImageNode() {
        In = Input(
            new BlobPortBuilder<IBlob<ImageMeta, IStreamData>>(nameof(In))
                .Input()
        );
        Out = Output(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(Out))
                .Output()
        );

        UseCache = false;
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var input = In.Read(context)!;

        var stream = input.Storage.OpenRead();
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        Out.Write(context, new Blob<ImageMeta, IBufferData>(input.Meta, new BufferData(ms.ToArray())));
        return true;
    }
}
