using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;
using Shiron.Lib.Pipeline.Types.Meta;
using SixLabors.ImageSharp.Metadata;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class DecodeImageNode : AbstractNode {
    public IInputPort<IBlob> In { get; }
    public IOutputPort<IBlob<ImageMeta, IStreamData>> Out { get; }

    public DecodeImageNode() {
        In = Input(
            new BlobPortBuilder<IBlob>(nameof(In))
                .Input()
        );
        Out = Output(
            new BlobPortBuilder<IBlob<ImageMeta, IStreamData>>(nameof(Out))
                .Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var blob = In.Read(context)!;

        // TODO: Read image metadata from blob
        var meta = new ImageMeta(1920, 1080);
        var image = new Blob<ImageMeta, IStreamData>(meta, blob.Storage);

        Out.Write(context, image);
        return ValueTask.FromResult(true);
    }
}
