using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Shiron.Backflow.Types;

namespace Shiron.Backflow.Samples.Nodes;

public class SaveFileNode : AbstractNode {
    public IInputPort<IBlob> Data { get; }
    public IInputPort<string> FileName { get; }

    public SaveFileNode() {
        Data = Input(
            new BlobPortBuilder<IBlob>(nameof(Data))
                .Input()
        );
        FileName = Input(
            new StringPortBuilder(nameof(FileName))
                .MaxLength(255)
                .MinLength(1)
                .Input()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var fileName = FileName.Read(context)!;
        var data = Data.Read(context)!;

        await using var dataStream = data.Storage.OpenRead();
        await using var fileStream = File.OpenWrite(fileName);
        await dataStream.CopyToAsync(fileStream);
        return true;
    }
}
