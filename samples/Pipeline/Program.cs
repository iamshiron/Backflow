using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Serialization;
using Shiron.Lib.Samples.Pipeline.Nodes;

if (!Directory.Exists(".output")) {
    Directory.CreateDirectory(".output");
}

var registry = new NodeRegistry();
var addNode = registry.Register<AddNode>();
var printNode = registry.Register<PrintNode>();
var subtractNode = registry.Register<SubtractNode>();
var concatNode = registry.Register<ConcatNode>();

var builder = new PipelineBuilder(registry);
var addInstance = builder.AddNode(addNode);
var subtractInstance = builder.AddNode(subtractNode);
var printInstance = builder.AddNode(printNode);
var printInstance2 = builder.AddNode(printNode);
var concatInstance = builder.AddNode(concatNode);
var printInstanceCat = builder.AddNode(printNode);

builder.AddConnection(
    addInstance, addNode.Sum,
    printInstance, printNode.Message
);

builder.AddConnection(
    addInstance, addNode.Sum,
    subtractInstance, subtractNode.Number2
);

builder.AddConnection(
    subtractInstance, subtractNode.Diff,
    printInstance2, printNode.Message
);

builder.AddConnection(
    concatInstance, concatNode.Concatenated,
    printInstanceCat, printNode.Message
);

PipelineContext context = new PipelineContext();
context.Write<int>(addInstance, addNode.Number1, 19);
context.Write<int>(addInstance, addNode.Number2, 95);
context.Write<int>(subtractInstance, subtractNode.Number1, 100);
context.Write<string>(printInstance, printNode.Prefix, "Result 1: ");
context.Write<string>(printInstance2, printNode.Prefix, "Result 2: ");
context.Write<string>(printInstanceCat, printNode.Prefix, "Concatenation: ");
context.Write<string>(concatInstance, concatNode.String1, "Hello ");
context.Write<string>(concatInstance, concatNode.String2, "World!");

Console.WriteLine($"Port 1: {context.Read<int>(addInstance, addNode.Number1)}");
Console.WriteLine($"Port 2: {context.Read<int>(addInstance, addNode.Number2)}");
Console.WriteLine($"Port 3: {context.Read<int>(subtractInstance, subtractNode.Number1)}");
Console.WriteLine($"Port 4: {context.Read<string>(printInstance, printNode.Message)}");
Console.WriteLine($"Port 5: {context.Read<string>(printInstanceCat, printNode.Message)}");

var pipeline = builder.Build();
var executor = new PipelineExecutor(pipeline);

Console.WriteLine($"Executing {executor.Layers.Length} layers");
for (var i = 0; i < executor.Layers.Length; ++i) {
    Console.WriteLine($"Layer {i}: {executor.Layers[i].Length} - {string.Join(", ", executor.Layers[i].Select(n => n.Node.GetType().FullName))}");
}

File.WriteAllText(".output/graph.json", pipeline.Serialize(context, new JsonSerializerOptions {
    WriteIndented = true,
    IndentSize = 4
}));
await executor.ExecuteAsync(context);
