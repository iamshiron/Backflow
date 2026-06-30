using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Port.Builder;
using Shiron.Backflow.Types;

namespace Shiron.Backflow.Samples.Nodes;

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

        Data.Write(context, new RawBlob(new StreamData(() => File.OpenRead(fileName))));
        return true;
    }
}
