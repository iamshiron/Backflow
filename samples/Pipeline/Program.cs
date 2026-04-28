using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Serialization;
using Shiron.Lib.Samples.Pipeline.Nodes;

var registry = new NodeRegistry();
var addNode = registry.Register<AddNode>();
var printNode = registry.Register<PrintNode>();
var subtractNode = registry.Register<SubtractNode>();

var builder = new PipelineBuilder(registry);
var addInstance = builder.AddNode(addNode);
var subtractInstance = builder.AddNode(subtractNode);
var printInstance = builder.AddNode(printNode);
var printInstance2 = builder.AddNode(printNode);

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

IPipelineContext context = new PipelineContext();
context.Write(addInstance, addNode.Number1, 19);
context.Write(addInstance, addNode.Number2, 95);
context.Write(subtractInstance, subtractNode.Number1, 100);

Console.WriteLine($"Port 1: {context.Read(addInstance, addNode.Number1)}");
Console.WriteLine($"Port 2: {context.Read(addInstance, addNode.Number2)}");
Console.WriteLine($"Port 3: {context.Read(subtractInstance, subtractNode.Number1)}");

var pipeline = builder.Build();
var executor = new PipelineExecutor(pipeline);

Console.WriteLine($"Executing {executor.Layers.Length} layers");
for (var i = 0; i < executor.Layers.Length; ++i) {
    Console.WriteLine($"Layer {i}: {executor.Layers[i].Length} - {string.Join(", ", executor.Layers[i].Select(n => n.Node.GetType().FullName))}");
}

await executor.ExecuteAsync(context);
