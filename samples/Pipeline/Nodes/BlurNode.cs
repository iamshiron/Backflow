using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Types;
using Shiron.Lib.Samples.Pipeline.Types.Meta;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class BlurNode : AbstractNode {
    public IInputPort<IBlob<ImageMeta, IBufferData>> In { get; }
    public IInputPort<int> Radius { get; }

    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

    public BlurNode() {
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

        Console.WriteLine($"Data: {data}");
        Console.WriteLine($"Stream: {data.Storage}");

        using var image = await Image.LoadAsync(data.Storage.OpenRead());
        image.Mutate(i => i.GaussianBlur(radius));

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);

        Out.Write(context, new Blob<ImageMeta, IBufferData>(data.Meta, new BufferData(ms.ToArray())));

        return true;
    }
}
