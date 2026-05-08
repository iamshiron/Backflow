using Shiron.Lib.Pipeline;
using Shiron.Lib.Samples.Pipeline.Nodes;

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
    public DecodeImageNode DecodeImage { get; }

    public GlobalNodeRegistry() {
        Print = Registry.Register<PrintNode>();
        Add = Registry.Register<AddNode>();
        Subtract = Registry.Register<SubtractNode>();
        Concat = Registry.Register<ConcatNode>();
        AddSub = Registry.Register<AddSubNode>();
        Blur = Registry.Register<BlurNode>();
        SaveFile = Registry.Register<SaveFileNode>();
        ReadFile = Registry.Register<ReadFileNode>();
        DecodeImage = Registry.Register<DecodeImageNode>();
    }
}
