using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Registry;
using Shiron.Lib.Samples.Pipeline.Nodes;
using Shiron.Lib.Samples.Pipeline.Nodes.Generic;
using Shiron.Lib.Samples.Pipeline.Nodes.Json;

namespace Shiron.Lib.Samples.Pipeline;

public class GlobalNodeRegistry {
    public NodeRegistry Registry { get; }

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
    public WebFetchNode WebFetch { get; }
    public GetJsonElementNode GetJsonElement { get; }
    public JsonElementIntNode JsonElementInt { get; }
    public ComparisonNode Comparison { get; }
    public IntRangeArrayNode IntRangeArray { get; }
    public IntArrayElementAtNode IntArrayElementAt { get; }
    public IntArrayLengthNode IntArrayLength { get; }
    public IntAverageNode IntAverage { get; }
    public DoubleMultiplierNode DoubleMultiplier { get; }

    public NodeBlueprint GenericAdd { get; }

    public GlobalNodeRegistry(INodeActivator? provider = null) {
        Registry = new NodeRegistry(provider);

        Print = Registry.Register<PrintNode>("I/O");
        Add = Registry.Register<AddNode>("Math");
        Subtract = Registry.Register<SubtractNode>("Math");
        Concat = Registry.Register<ConcatNode>("String");
        AddSub = Registry.Register<AddSubNode>("Math");
        Blur = Registry.Register<BlurNode>("Image");
        SaveFile = Registry.Register<SaveFileNode>("I/O");
        ReadFile = Registry.Register<ReadFileNode>("I/O");
        Bufferize = Registry.Register<BufferizeNode>("Blobs");
        DecodeImage = Registry.Register<DecodeImageNode>("Image");
        ImageInfo = Registry.Register<ImageInfoNode>("Image");
        GrayScale = Registry.Register<GrayScaleNode>("Image");
        PackVector2 = Registry.Register<PackVector2Node>("Vector");
        UnpackVector2 = Registry.Register<UnpackVector2Node>("Vector");
        PackVector3 = Registry.Register<PackVector3Node>("Vector");
        UnpackVector3 = Registry.Register<UnpackVector3Node>("Vector");
        PackVector4 = Registry.Register<PackVector4Node>("Vector");
        UnpackVector4 = Registry.Register<UnpackVector4Node>("Vector");
        Greet = Registry.Register<GreetNode>("Misc");
        WebFetch = Registry.Register<WebFetchNode>("I/O");
        GetJsonElement = Registry.Register<GetJsonElementNode>("JSON");
        JsonElementInt = Registry.Register<JsonElementIntNode>("JSON");
        Comparison = Registry.Register<ComparisonNode>("Math");
        IntRangeArray = Registry.Register<IntRangeArrayNode>("Array");
        IntArrayElementAt = Registry.Register<IntArrayElementAtNode>("Array");
        IntArrayLength = Registry.Register<IntArrayLengthNode>("Array");
        IntAverage = Registry.Register<IntAverageNode>("Math");
        DoubleMultiplier = Registry.Register<DoubleMultiplierNode>("Math");

        GenericAdd = Registry.RegisterGeneric(typeof(GenericAddNode<>), "Math");
    }
}
