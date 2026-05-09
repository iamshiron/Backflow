using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;
using Shiron.Lib.Pipeline.Types.Meta;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class GrayScaleNode : AbstractNode {
    public IInputPort<IBlob<ImageMeta, IBufferData>> In { get; }
    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

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

        Console.WriteLine($"Data: {data}");
        Console.WriteLine($"Stream: {data.Storage}");

        using var image = await Image.LoadAsync(data.Storage.OpenRead());
        image.Mutate(i => i.Grayscale());

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);

        Out.Write(context, new Blob<ImageMeta, IBufferData>(data.Meta, new BufferData(ms.ToArray())));
        return true;
    }
}
