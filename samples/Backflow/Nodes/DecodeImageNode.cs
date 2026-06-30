using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Types;
using Shiron.Backflow.Samples.Types;
using Shiron.Backflow.Samples.Types.Meta;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Shiron.Backflow.Samples.Nodes;

public class DecodeImageNode : AbstractNode {
    public IInputPort<IBlob> In { get; }
    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

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

        var info = await Image.IdentifyAsync(storage.OpenRead());

        var frameCount = info.FrameMetadataCollection.Count;
        var hasAlpha = info.PixelType?.AlphaRepresentation
            is not (PixelAlphaRepresentation.None
            or null);

        var meta = new ImageMeta(
            info.Width,
            info.Height,
            info.Metadata.DecodedImageFormat?.Name,
            info.PixelType?.ToString(),
            info.PixelType?.BitsPerPixel,
            info.Metadata.HorizontalResolution,
            info.Metadata.VerticalResolution,
            hasAlpha,
            frameCount > 1,
            frameCount > 1 ? frameCount : null
        );

        Out.Write(context, new Blob<ImageMeta, IBufferData>(meta, storage));
        return true;
    }
}
