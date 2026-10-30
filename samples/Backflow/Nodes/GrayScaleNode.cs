using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Types;
using Shiron.Backflow.Samples.Types;
using Shiron.Backflow.Samples.Types.Meta;
using Shiron.Backflow.Samples.Imaging;

namespace Shiron.Backflow.Samples.Nodes;

/// <summary>Converts image pixels to BT.709 grayscale and emits an independent PNG buffer.</summary>
public sealed class GrayScaleNode : AbstractNode {
    private const float RedLuminance = 0.2126f;
    private const float GreenLuminance = 0.7152f;
    private const float BlueLuminance = 0.0722f;
    private const int ChannelsPerPixel = 4;

    /// <summary>The encoded input image.</summary>
    public IInputPort<IBlob<ImageMeta, IBufferData>> In { get; }
    /// <summary>The grayscale PNG image.</summary>
    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

    /// <summary>Creates the grayscale ports.</summary>
    public GrayScaleNode() {
        In = Input(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(In))
                .Input()
        );
        Out = Output(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(Out))
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var data = In.Read(context)!;

        var decoded = await SampleImageCodec.DecodeAsync(data.Storage, context.CancellationToken);
        using var image = decoded.Bitmap;
        var pixels = image.GetPixelSpan();
        for (var y = 0; y < image.Height; y++) {
            context.CancellationToken.ThrowIfCancellationRequested();
            var row = pixels.Slice(y * image.RowBytes, image.Width * ChannelsPerPixel);
            for (var x = 0; x < row.Length; x += ChannelsPerPixel) {
                var luminance = (byte) MathF.Round(row[x] * RedLuminance + row[x + 1] * GreenLuminance + row[x + 2] * BlueLuminance);
                row[x] = luminance;
                row[x + 1] = luminance;
                row[x + 2] = luminance;
            }
        }
        Out.Write(context, SampleImageCodec.EncodePng(image, context.CancellationToken));
        return true;
    }
}
