using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Types;
using Shiron.Backflow.Samples.Types;
using Shiron.Backflow.Samples.Types.Meta;
using Shiron.Backflow.Samples.Imaging;

namespace Shiron.Backflow.Samples.Nodes;

/// <summary>Validates encoded image pixels and describes them without replacing the input storage.</summary>
public sealed class DecodeImageNode : AbstractNode {
    /// <summary>The encoded image buffer.</summary>
    public IInputPort<IBlob> In { get; }
    /// <summary>The original image buffer with decoded metadata.</summary>
    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

    /// <summary>Creates the image decoder ports.</summary>
    public DecodeImageNode() {
        In = Input(
            new BlobPortBuilder<IBlob>(nameof(In))
                .Input()
        );
        Out = Output(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(Out))
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var blob = In.Read(context)!;
        var storage = (IBufferData) blob.Storage;

        var decoded = await SampleImageCodec.DecodeAsync(storage, context.CancellationToken);
        using var bitmap = decoded.Bitmap;
        Out.Write(context, new Blob<ImageMeta, IBufferData>(decoded.Meta, storage));
        return true;
    }
}
