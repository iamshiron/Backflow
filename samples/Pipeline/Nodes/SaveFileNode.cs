using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

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
        var data = Data.Read(context)!.Data;

        Console.WriteLine($"Saving file to {fileName}, Size {data.Length}");

        await File.WriteAllBytesAsync(fileName, data);

        return true;
    }
}
