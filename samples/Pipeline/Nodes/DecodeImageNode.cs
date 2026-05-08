using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class DecodeImageNode : AbstractNode {
    public IInputPort<IBlob> Data { get; }
    public IOutputPort<IImageBlob> Out { get; }

    public DecodeImageNode() {
        Data = Input(
            new BlobPortBuilder<IBlob>(nameof(Data))
                .Input()
        );
        Out = Output(
            new BlobPortBuilder<IImageBlob>(nameof(Out))
                .Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var blob = Data.Read(context);
        if (blob == null) {
            return ValueTask.FromResult(false);
        }

        var image = blob as IImageBlob;
        if (image == null) return ValueTask.FromResult(false);

        Out.Write(context, image);
        return ValueTask.FromResult(true);
    }
}
