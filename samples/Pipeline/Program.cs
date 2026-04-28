using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Samples.Pipeline.Nodes;

var registry = new NodeRegistry();
var addNode = registry.Register<AddNode>();
var printNode = registry.Register<PrintNode>();

var builder = new PipelineBuilder(registry);
var addInstance = builder.AddNode(addNode);
var printInstance = builder.AddNode(printNode);

builder.AddConnection(
    addInstance, addNode.Sum,
    printInstance, printNode.Message
);

IPipelineContext context = new PipelineContext();
context.Write(addInstance, addNode.Number1, 19);
context.Write(addInstance, addNode.Number2, 95);

Console.WriteLine($"Port 1: {context.Read(addInstance, addNode.Number1)}");
Console.WriteLine($"Port 2: {context.Read(addInstance, addNode.Number2)}");

var executor = new PipelineExecutor(builder.Build());
executor.Execute(context);
