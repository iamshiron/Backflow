using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class ReadFileNode : AbstractNode {
    public IInputPort<string> FileName { get; }
    public IOutputPort<IImageBlob> Data { get; }

    public ReadFileNode() {
        FileName = Input(
            new StringPortBuilder(nameof(FileName))
                .MaxLength(255)
                .MinLength(1)
                .Input()
        );

        Data = Output(
            new BlobPortBuilder<IImageBlob>(nameof(Data))
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var fileName = FileName.Read(context)!;

        Console.WriteLine($"Reading file {fileName}");
        var blob = new MemoryBlob();
        blob.Data = await File.ReadAllBytesAsync(fileName);
        Data.Write(context, blob);

        return true;
    }
}
