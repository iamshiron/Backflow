using Shiron.Lib.Pipeline;
using Shiron.Lib.Samples.Pipeline.Nodes;
using Shiron.Lib.Pipeline.Types.Meta;

namespace Shiron.Lib.Samples.Pipeline;

public class GlobalNodeRegistry {
    public NodeRegistry Registry { get; } = new();

    public PrintNode Print { get; }
    public AddNode Add { get; }
    public SubtractNode Subtract { get; }
    public ConcatNode Concat { get; }
    public AddSubNode AddSub { get; }
    public BlurNode Blur { get; }
    public SaveFileNode SaveFile { get; }
    public ReadFileNode ReadFile { get; }
    public BufferizeNode Bufferize { get; }
    public DecodeImageNode DecodeImage { get; }
    public GrayScaleNode GrayScale { get; }

    public GlobalNodeRegistry() {
        Print = Registry.Register<PrintNode>();
        Add = Registry.Register<AddNode>();
        Subtract = Registry.Register<SubtractNode>();
        Concat = Registry.Register<ConcatNode>();
        AddSub = Registry.Register<AddSubNode>();
        Blur = Registry.Register<BlurNode>();
        SaveFile = Registry.Register<SaveFileNode>();
        ReadFile = Registry.Register<ReadFileNode>();
        Bufferize = Registry.Register<BufferizeNode>();
        DecodeImage = Registry.Register<DecodeImageNode>();
        GrayScale = Registry.Register<GrayScaleNode>();
    }
}
