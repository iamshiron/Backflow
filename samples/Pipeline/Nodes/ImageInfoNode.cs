using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;
using Shiron.Lib.Pipeline.Types.Meta;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class ImageInfoNode : AbstractNode {
    public IInputPort<IBlob<ImageMeta, IBufferData>> In { get; }
    public IOutputPort<int> Width { get; }
    public IOutputPort<int> Height { get; }
    public IOutputPort<string> Format { get; }
    public IOutputPort<string> PixelFormat { get; }
    public IOutputPort<int> BitsPerPixel { get; }
    public IOutputPort<double> DpiX { get; }
    public IOutputPort<double> DpiY { get; }
    public IOutputPort<bool> HasAlpha { get; }
    public IOutputPort<bool> IsAnimated { get; }
    public IOutputPort<int> FrameCount { get; }

    public ImageInfoNode() {
        In = Input(
            new BlobPortBuilder<IBlob<ImageMeta, IBufferData>>(nameof(In))
                .Input()
        );
        Width = Output(new NumericPortBuilder<int>(nameof(Width)).Output());
        Height = Output(new NumericPortBuilder<int>(nameof(Height)).Output());
        Format = Output(new StringPortBuilder(nameof(Format)).Output());
        PixelFormat = Output(new StringPortBuilder(nameof(PixelFormat)).Output());
        BitsPerPixel = Output(new NumericPortBuilder<int>(nameof(BitsPerPixel)).Output());
        DpiX = Output(new NumericPortBuilder<double>(nameof(DpiX)).Output());
        DpiY = Output(new NumericPortBuilder<double>(nameof(DpiY)).Output());
        HasAlpha = Output(new BoolPortBuilder(nameof(HasAlpha)).Output());
        IsAnimated = Output(new BoolPortBuilder(nameof(IsAnimated)).Output());
        FrameCount = Output(new NumericPortBuilder<int>(nameof(FrameCount)).Output());
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var meta = In.Read(context)!.Meta;

        Width.Write(context, meta.Width);
        Height.Write(context, meta.Height);
        Format.Write(context, meta.Format ?? string.Empty);
        PixelFormat.Write(context, meta.PixelFormat ?? string.Empty);
        BitsPerPixel.Write(context, meta.BitsPerPixel ?? 0);
        DpiX.Write(context, meta.DpiX ?? 0.0);
        DpiY.Write(context, meta.DpiY ?? 0.0);
        HasAlpha.Write(context, meta.HasAlpha);
        IsAnimated.Write(context, meta.IsAnimated);
        FrameCount.Write(context, meta.FrameCount ?? 0);
        return ValueTask.FromResult(true);
    }
}
