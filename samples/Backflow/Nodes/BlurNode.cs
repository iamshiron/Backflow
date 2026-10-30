using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Shiron.Backflow.Types;
using Shiron.Backflow.Samples.Types;
using Shiron.Backflow.Samples.Types.Meta;
using Shiron.Backflow.Samples.Imaging;

namespace Shiron.Backflow.Samples.Nodes;

/// <summary>Applies a Gaussian blur with clamped edges and emits an independent PNG buffer.</summary>
public sealed class BlurNode : AbstractNode {
    /// <summary>The encoded input image.</summary>
    public IInputPort<IBlob<ImageMeta, IBufferData>> In { get; }
    /// <summary>The nonnegative Gaussian sigma in pixels; zero leaves pixels unchanged.</summary>
    public IInputPort<int> Radius { get; }

    /// <summary>The blurred PNG image.</summary>
    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

    /// <summary>Creates the image and sigma ports.</summary>
    public BlurNode() {
        UseCache = true;

        In = Input(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(In))
                .Input()
        );
        Radius = Input(
            new NumericPortBuilder<int>(nameof(Radius))
                .Default(5)
                .Input()
        );

        Out = Output(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(Out))
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var data = In.Read(context)!;
        var radius = Radius.Read(context);

        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var decoded = await SampleImageCodec.DecodeAsync(data.Storage, context.CancellationToken);
        using var image = decoded.Bitmap;
        if (radius == 0) {
            Out.Write(context, SampleImageCodec.EncodePng(image, context.CancellationToken));
            return true;
        }

        using var result = GaussianBlur.Apply(image, radius, context.CancellationToken);
        Out.Write(context, SampleImageCodec.EncodePng(result, context.CancellationToken));

        return true;
    }
}
