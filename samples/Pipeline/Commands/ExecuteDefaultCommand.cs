using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Serialization;
using Shiron.Lib.Samples.Pipeline.Nodes;
using Shiron.Lib.Samples.Pipeline.Types;
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
            var readFileInstance = builder.AddNode(registry.ReadFile);
            var bufferizeInstance = builder.AddNode(registry.Bufferize);
            var decodeImageInstance = builder.AddNode(registry.DecodeImage);
            var blurInstance = builder.AddNode(registry.Blur);
            var saveFileInstance = builder.AddNode(registry.SaveFile);
            var blurInstance2 = builder.AddNode(registry.Blur);
            var saveFileInstance2 = builder.AddNode(registry.SaveFile);
            var saveFileInstance3 = builder.AddNode(registry.SaveFile);
            var grayScaleInstance = builder.AddNode(registry.GrayScale);
            var imageInfoInstance = builder.AddNode(registry.ImageInfo);
            var printInstanceWidth = builder.AddNode(registry.Print);
            var printInstanceHeight = builder.AddNode(registry.Print);
            var packVector2Instance = builder.AddNode(registry.PackVector2);
            var printVector2Instance = builder.AddNode(registry.Print);
            var packVector3Instance = builder.AddNode(registry.PackVector3);
            var printVector3Instance = builder.AddNode(registry.Print);
            var packVector4Instance = builder.AddNode(registry.PackVector4);
            var printVector4Instance = builder.AddNode(registry.Print);
            var greetInstance = builder.AddNode(registry.Greet);
            var printInstanceGreet = builder.AddNode(registry.Print);
            var webFetchInstance = builder.AddNode(registry.WebFetch);
            var printWebFetchInstance = builder.AddNode(registry.Print);
            var printWebFetchCodeInstance = builder.AddNode(registry.Print);
            var getJsonElementInstance = builder.AddNode(registry.GetJsonElement);
            var jsonElementIntInstance = builder.AddNode(registry.JsonElementInt);
            var printJsonElementIntInstance = builder.AddNode(registry.Print);
            var compareInstance = builder.AddNode(registry.Comparison);
            var printCompareInstance = builder.AddNode(registry.Print);
            var intRangeArrayInstance = builder.AddNode(registry.IntRangeArray);
            var printRangeArrayInstance = builder.AddNode(registry.Print);
            var intArrayElementAtInstance = builder.AddNode(registry.IntArrayElementAt);
            var printIntArrayElementAtInstance = builder.AddNode(registry.Print);
            var intArrayLengthInstance = builder.AddNode(registry.IntArrayLength);
            var printIntArrayLengthInstance = builder.AddNode(registry.Print);

            var intAverageInstance = builder.AddNode(registry.IntAverage, new Dictionary<string, int> { ["Values"] = 5 });
            var printIntAverageInstance = builder.AddNode(registry.Print);

            // Implicit Cast Demo: int → double (lossless)
            var doubleMultiplierInstance = builder.AddNode(registry.DoubleMultiplier);
            var printDoubleResultInstance = builder.AddNode(registry.Print);
            // Implicit Cast Demo: double → int (lossy)
            var addFromDoubleInstance = builder.AddNode(registry.Add);
            var printLossyCastInstance = builder.AddNode(registry.Print);

            var genericAddRef = builder.AddNode(registry.GenericAdd);
            var genericPrintAdd = builder.AddNode(registry.Print);

            builder.AddConnection(
                greetInstance, registry.Greet.Greeting,
                printInstanceGreet, registry.Print.Message
            );

            builder.AddConnection(
                addInstance, registry.Add.Sum,
                genericAddRef, genericAddRef.Port("Number1")
            );
            builder.AddConnection(
                addInstance, registry.Add.Sum,
                genericAddRef, genericAddRef.Port("Number2")
            );
            builder.AddConnection(
                genericAddRef, genericAddRef.Port("Sum"),
                genericPrintAdd, registry.Print.Message
            );

            builder.AddConnection(
                readFileInstance, registry.ReadFile.Data,
                bufferizeInstance, registry.Bufferize.In
            );
            builder.AddConnection(
                bufferizeInstance, registry.Bufferize.Out,
                decodeImageInstance, registry.DecodeImage.In
            );
            builder.AddConnection(
                decodeImageInstance, registry.DecodeImage.Out,
                blurInstance, registry.Blur.In
            );
            builder.AddConnection(
                decodeImageInstance, registry.DecodeImage.Out,
                imageInfoInstance, registry.ImageInfo.In
            );
            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Width,
                printInstanceWidth, registry.Print.Message
            );
            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Height,
                printInstanceHeight, registry.Print.Message
            );
            builder.AddConnection(
                blurInstance, registry.Blur.Out,
                saveFileInstance, registry.SaveFile.Data
            );
            builder.AddConnection(
                blurInstance, registry.Blur.Out,
                grayScaleInstance, registry.GrayScale.In
            );
            builder.AddConnection(
                grayScaleInstance, registry.GrayScale.Out,
                saveFileInstance3, registry.SaveFile.Data
            );
            builder.AddConnection(
                blurInstance, registry.Blur.Out,
                blurInstance2, registry.Blur.In
            );
            builder.AddConnection(
                blurInstance2, registry.Blur.Out,
                saveFileInstance2, registry.SaveFile.Data
            );
            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Width,
                packVector2Instance, registry.PackVector2.X
            );
            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Height,
                packVector2Instance, registry.PackVector2.Y
            );
            builder.AddConnection(
                packVector2Instance, registry.PackVector2.Out,
                printVector2Instance, registry.Print.Message
            );

            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Width,
                packVector3Instance, registry.PackVector3.X
            );
            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Height,
                packVector3Instance, registry.PackVector3.Y
            );
            builder.AddConnection(
                packVector3Instance, registry.PackVector3.Out,
                printVector3Instance, registry.Print.Message
            );

            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Width,
                packVector4Instance, registry.PackVector4.X
            );
            builder.AddConnection(
                imageInfoInstance, registry.ImageInfo.Height,
                packVector4Instance, registry.PackVector4.Y
            );
            builder.AddConnection(
                packVector4Instance, registry.PackVector4.Out,
                printVector4Instance, registry.Print.Message
            );

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

            // Json Tests
            builder.AddConnection(
                webFetchInstance, registry.WebFetch.Response,
                printWebFetchInstance, registry.Print.Message
            );
            builder.AddConnection(
                webFetchInstance, registry.WebFetch.ResponseCode,
                printWebFetchCodeInstance, registry.Print.Message
            );
            builder.AddConnection(
                webFetchInstance, registry.WebFetch.Response,
                getJsonElementInstance, registry.GetJsonElement.Json
            );
            builder.AddConnection(
                getJsonElementInstance, registry.GetJsonElement.Element,
                jsonElementIntInstance, registry.JsonElementInt.Element
            );
            builder.AddConnection(
                jsonElementIntInstance, registry.JsonElementInt.Element,
                printJsonElementIntInstance, registry.Print.Message
            );

            // Compare
            builder.AddConnection(
                addInstance, registry.Add.Sum,
                compareInstance, registry.Comparison.A
            );
            builder.AddConnection(
                subtractInstance, registry.Subtract.Diff,
                compareInstance, registry.Comparison.B
            );
            builder.AddConnection(
                compareInstance, registry.Comparison.Result,
                printCompareInstance, registry.Print.Message
            );

            // Array
            builder.AddConnection(
                intRangeArrayInstance, registry.IntRangeArray.Out,
                printRangeArrayInstance, registry.Print.Message
            );
            builder.AddConnection(
                intRangeArrayInstance, registry.IntRangeArray.Out,
                intArrayElementAtInstance, registry.IntArrayElementAt.Array
            );
            builder.AddConnection(
                intArrayElementAtInstance, registry.IntArrayElementAt.Out,
                printIntArrayElementAtInstance, registry.Print.Message
            );
            builder.AddConnection(
                intRangeArrayInstance, registry.IntRangeArray.Out,
                intArrayLengthInstance, registry.IntArrayLength.Array
            );
            builder.AddConnection(
                intArrayLengthInstance, registry.IntArrayLength.Length,
                printIntArrayLengthInstance, registry.Print.Message
            );

            builder.AddConnection(
                intAverageInstance, registry.IntAverage.Average,
                printIntAverageInstance, registry.Print.Message
            );

            // Implicit Cast Demo
            // Lossless: AddNode.Sum (int) → DoubleMultiplierNode.Value (double)
            builder.AddConnection(
                addInstance, registry.Add.Sum,
                doubleMultiplierInstance, registry.DoubleMultiplier.Value
            );
            // Lossless: DoubleMultiplierNode.Result (double) → PrintNode.Message (object)
            builder.AddConnection(
                doubleMultiplierInstance, registry.DoubleMultiplier.Result,
                printDoubleResultInstance, registry.Print.Message
            );
            // Lossy: DoubleMultiplierNode.Result (double) → AddNode.Number1 (int)
            builder.AddConnection(
                doubleMultiplierInstance, registry.DoubleMultiplier.Result,
                addFromDoubleInstance, registry.Add.Number1
            );
            // Lossy result → PrintNode
            builder.AddConnection(
                addFromDoubleInstance, registry.Add.Sum,
                printLossyCastInstance, registry.Print.Message
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
            context.Write<bool>(addChipEnableInstance, registry.Add.ChipEnableBehavior.ChipEnable, true);
            context.Write<string>(printInstanceChipEnable, registry.Print.Prefix, "Chip Enable: ");

            // Image Sub Nodes
            context.Write<string>(readFileInstance, registry.ReadFile.FileName, "./.output/image.png");
            context.Write<string>(saveFileInstance, registry.SaveFile.FileName, "./.output/image-blur.png");
            context.Write<string>(saveFileInstance2, registry.SaveFile.FileName, "./.output/image-blur-2.png");
            context.Write<string>(saveFileInstance3, registry.SaveFile.FileName, "./.output/image-grayscale.png");
            context.Write<int>(blurInstance, registry.Blur.Radius, 4);
            context.Write<int>(blurInstance2, registry.Blur.Radius, 32);
            context.Write<string>(printInstanceWidth, registry.Print.Prefix, "Image Width: ");
            context.Write<string>(printInstanceHeight, registry.Print.Prefix, "Image Height: ");

            // Vector Pack Demo
            context.Write<int>(packVector2Instance, registry.PackVector2.X, 10);
            context.Write<int>(packVector2Instance, registry.PackVector2.Y, 20);
            context.Write<int>(packVector3Instance, registry.PackVector3.X, 10);
            context.Write<int>(packVector3Instance, registry.PackVector3.Y, 20);
            context.Write<int>(packVector3Instance, registry.PackVector3.Z, 30);
            context.Write<int>(packVector4Instance, registry.PackVector4.X, 10);
            context.Write<int>(packVector4Instance, registry.PackVector4.Y, 20);
            context.Write<int>(packVector4Instance, registry.PackVector4.Z, 30);
            context.Write<int>(packVector4Instance, registry.PackVector4.W, 40);
            context.Write<string>(printVector2Instance, registry.Print.Prefix, "Vector2: ");
            context.Write<string>(printVector3Instance, registry.Print.Prefix, "Vector3: ");
            context.Write<string>(printVector4Instance, registry.Print.Prefix, "Vector4: ");

            context.Write<TimeOfDay>(greetInstance, registry.Greet.TimeOfDay, TimeOfDay.Night);
            context.Write<string>(printInstanceGreet, registry.Print.Prefix, "Greeting: ");

            context.Write<string>(genericPrintAdd, registry.Print.Prefix, "Generic Add: ");

            // Web Fetch
            context.Write<string>(webFetchInstance, registry.WebFetch.Url, "https://jsonplaceholder.typicode.com/posts/42");
            context.Write<string>(webFetchInstance, registry.WebFetch.Method, "GET");
            context.Write<string>(printWebFetchInstance, registry.Print.Prefix, "Web Fetch: ");
            context.Write<string>(printWebFetchCodeInstance, registry.Print.Prefix, "Web Fetch Code: ");
            context.Write<string>(getJsonElementInstance, registry.GetJsonElement.Path, "userId");
            context.Write<string>(printJsonElementIntInstance, registry.Print.Prefix, "Json Element Int: ");

            // Compare
            context.Write<ComparisonOperator>(compareInstance, registry.Comparison.Operator, ComparisonOperator.GreaterThan);
            context.Write<string>(printCompareInstance, registry.Print.Prefix, "Compare: ");

            // Array
            context.Write(intRangeArrayInstance, registry.IntRangeArray.Size, 27);
            context.Write<string>(printRangeArrayInstance, registry.Print.Prefix, "Range Array: ");
            context.Write<int>(intArrayElementAtInstance, registry.IntArrayElementAt.Index, 10);
            context.Write<string>(printIntArrayElementAtInstance, registry.Print.Prefix, "Array Element At: ");
            context.Write<string>(printIntArrayLengthInstance, registry.Print.Prefix, "Array Length: ");

            context.Write<int[]>(intAverageInstance, (IPort) registry.IntAverage.Values, [10, 20, 30, 40, 50]);
            context.Write<string>(printIntAverageInstance, registry.Print.Prefix, "Average: ");

            // Implicit Cast Demo inputs
            context.Write<double>(doubleMultiplierInstance, registry.DoubleMultiplier.Factor, 1.5);
            context.Write<string>(printDoubleResultInstance, registry.Print.Prefix, "Double Multiplier (int→double, lossless): ");
            context.Write<int>(addFromDoubleInstance, registry.Add.Number2, 100);
            context.Write<string>(printLossyCastInstance, registry.Print.Prefix, "Lossy Cast (double→int, truncated): ");

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
