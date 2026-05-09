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
    public BufferizeNode Bufferize { get; }
    public DecodeImageNode DecodeImage { get; }
    public ImageInfoNode ImageInfo { get; }
    public GrayScaleNode GrayScale { get; }
    public PackVector2Node PackVector2 { get; }
    public UnpackVector2Node UnpackVector2 { get; }
    public PackVector3Node PackVector3 { get; }
    public UnpackVector3Node UnpackVector3 { get; }
    public PackVector4Node PackVector4 { get; }
    public UnpackVector4Node UnpackVector4 { get; }
    public GreetNode Greet { get; }

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
        ImageInfo = Registry.Register<ImageInfoNode>();
        GrayScale = Registry.Register<GrayScaleNode>();
        PackVector2 = Registry.Register<PackVector2Node>();
        UnpackVector2 = Registry.Register<UnpackVector2Node>();
        PackVector3 = Registry.Register<PackVector3Node>();
        UnpackVector3 = Registry.Register<UnpackVector3Node>();
        PackVector4 = Registry.Register<PackVector4Node>();
        UnpackVector4 = Registry.Register<UnpackVector4Node>();
        Greet = Registry.Register<GreetNode>();
    }
}
