using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Shiron.Backflow.Types;
using Shiron.Backflow.Samples.Types;
using Shiron.Backflow.Samples.Types.Meta;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Shiron.Backflow.Samples.Nodes;

public class BlurNode : AbstractNode {
    public IInputPort<IBlob<ImageMeta, IBufferData>> In { get; }
    public IInputPort<int> Radius { get; }

    public IOutputPort<IBlob<ImageMeta, IBufferData>> Out { get; }

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
