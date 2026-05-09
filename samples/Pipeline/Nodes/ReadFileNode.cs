using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class ReadFileNode : AbstractNode {
    public IInputPort<string> FileName { get; }
    public IOutputPort<IBlob> Data { get; }

    public ReadFileNode() {
        FileName = Input(
            new StringPortBuilder(nameof(FileName))
                .MaxLength(255)
                .MinLength(1)
                .Input()
        );

        Data = Output(
            new BlobPortBuilder<IBlob>(nameof(Data))
                .Output()
        );
    }

    protected async override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var fileName = FileName.Read(context)!;
        if (!File.Exists(fileName)) {
            Console.WriteLine($"File {fileName} not found!");
            return false;
        }

        var fileStream = File.OpenRead(fileName);

        Data.Write(context, new RawBlob(new StreamData(() => fileStream)));
        return true;
    }
}
