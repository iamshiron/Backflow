using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class BlurNode : AbstractNode {
    public IInputPort<IImageBlob> Data { get; }
    public IInputPort<int> Radius { get; }

    public IOutputPort<IImageBlob> Out { get; }

    public BlurNode() {
        Data = Input(
            new BlobPortBuilder<IImageBlob>(nameof(Data))
                .Input()
        );
        Radius = Input(
            new NumericPortBuilder<int>(nameof(Radius))
                .Default(5)
                .Input()
        );

        Out = Output(
            new BlobPortBuilder<IImageBlob>(nameof(Out))
                .Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var data = Data.Read(context)!.Data;
        var radius = Radius.Read(context);

        using var image = Image.Load(data);
        image.Mutate(i => i.GaussianBlur(radius));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        Out.Write(context, new MemoryBlob {
            Data = stream.ToArray()
        });

        return ValueTask.FromResult(true);
    }
}
