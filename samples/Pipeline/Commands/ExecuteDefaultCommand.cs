using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Lib.Samples.Pipeline.Commands;

public class ExecuteDefaultCommand : AsyncCommand {
    protected async override Task<int> ExecuteAsync(CommandContext cmdContext, CancellationToken cancellationToken) {
        try {
            var registry = new GlobalNodeRegistry();

            var builder = new PipelineBuilder(registry.Registry);
            var addInstance = builder.AddNode(registry.Add);
            var subtractInstance = builder.AddNode(registry.Subtract);
            var printInstance = builder.AddNode(registry.Print);
            var printInstance2 = builder.AddNode(registry.Print);
            var printInstanceAddSub = builder.AddNode(registry.Print);
            var concatInstance = builder.AddNode(registry.Concat);
            var printInstanceCat = builder.AddNode(registry.Print);
            var addSubInstance = builder.AddNode(registry.AddSub);
            var addChipEnableInstance = builder.AddNode(registry.Add);
            var printInstanceChipEnable = builder.AddNode(registry.Print);
            var printInstanceAddEnableOut = builder.AddNode(registry.Print);

            builder.AddConnection(
                addInstance, registry.Add.Sum,
                printInstance, registry.Print.Message
            );

            builder.AddConnection(
                addChipEnableInstance, registry.Add.Sum,
                printInstanceChipEnable, registry.Print.Message
            );

            builder.AddConnection(
                addChipEnableInstance, registry.Add.EnableOutBehavior.EnableOut,
                printInstanceAddEnableOut, registry.Print.Message
            );

            builder.AddConnection(
                addInstance, registry.Add.Sum,
                subtractInstance, registry.Subtract.Number2
            );

            builder.AddConnection(
                subtractInstance, registry.Subtract.Diff,
                printInstance2, registry.Print.Message
            );

            builder.AddConnection(
                concatInstance, registry.Concat.Concatenated,
                printInstanceCat, registry.Print.Message
            );

            builder.AddConnection(
                addSubInstance, registry.AddSub.Result,
                printInstanceAddSub, registry.Print.Message
            );

            var context = new PipelineContext();
            context.Write<int>(addInstance, registry.Add.Number1, 19);
            context.Write<int>(addInstance, registry.Add.Number2, 95);
            context.Write<int>(subtractInstance, registry.Subtract.Number1, 100);
            context.Write<string>(printInstance, registry.Print.Prefix, "Result 1: ");
            context.Write<string>(printInstance2, registry.Print.Prefix, "Result 2: ");
            context.Write<string>(printInstanceCat, registry.Print.Prefix, "Concatenation: ");
            context.Write<string>(concatInstance, registry.Concat.String1, "Hello ");
            context.Write<string>(concatInstance, registry.Concat.String2, "World!");

            // Add Sub
            context.Write<int>(addSubInstance, registry.AddSub.Number1, 100);
            context.Write<int>(addSubInstance, registry.AddSub.Number2, 50);
            context.Write<bool>(addSubInstance, registry.AddSub.IsSubtract, true);
            context.Write<string>(printInstanceAddSub, registry.Print.Prefix, "Result Add Sub: ");
            context.Write<string>(printInstanceAddEnableOut, registry.Print.Prefix, "Result Add Enable Out: ");

            // Add Chip Enable
            context.Write<int>(addChipEnableInstance, registry.Add.Number1, 100);
            context.Write<int>(addChipEnableInstance, registry.Add.Number2, 50);
            context.Write<bool>(addChipEnableInstance, registry.Add.ChipEnableBehavior.ChipEnable, false);
            context.Write<string>(printInstanceChipEnable, registry.Print.Prefix, "Chip Enable: ");

            Console.WriteLine($"Port 1: {context.Read<int>(addInstance, registry.Add.Number1)}");
            Console.WriteLine($"Port 2: {context.Read<int>(addInstance, registry.Add.Number2)}");
            Console.WriteLine($"Port 3: {context.Read<int>(subtractInstance, registry.Subtract.Number1)}");
            Console.WriteLine($"Port 4: {context.Read<string>(concatInstance, registry.Concat.String1)}");
            Console.WriteLine($"Port 5: {context.Read<string>(concatInstance, registry.Concat.String2)}");

            var pipeline = builder.Build();
            var executor = new PipelineExecutor(pipeline);

            Console.WriteLine($"Executing {executor.Layers.Length} layers");
            for (var i = 0; i < executor.Layers.Length; ++i) {
                Console.WriteLine($"Layer {i}: {executor.Layers[i].Length} - {string.Join(", ", executor.Layers[i].Select(n => n.Node.GetType().FullName))}");
            }

            var jsonOptions = new JsonSerializerOptions {
                WriteIndented = true,
                IndentSize = 4
            };

            await File.WriteAllTextAsync(".output/graph.json", pipeline.SerializeDefinition(jsonOptions), cancellationToken);
            await File.WriteAllTextAsync(".output/inputs.json", pipeline.SerializeInputs(context, jsonOptions), cancellationToken);
            var stats = await executor.ExecuteAsync(context);
            Console.WriteLine(stats);
            return 0;
        } catch (Exception e) {
            AnsiConsole.WriteException(e);
            return 1;
        }
    }
}
