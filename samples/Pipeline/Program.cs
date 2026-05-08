using System.Text.Json;
using Shiron.Lib.Collections.Bucket;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Serialization;
using Shiron.Lib.Pipeline.Types;
using Shiron.Lib.Samples.Pipeline.Commands;
using Shiron.Lib.Samples.Pipeline.Nodes;
using Spectre.Console;
using Spectre.Console.Cli;

if (!Directory.Exists(".output")) {
    Directory.CreateDirectory(".output");
}

var app = new CommandApp();
app.Configure(c => {
    c.SetApplicationName("Pipeline Sample");
    c.SetApplicationVersion("0.0.0");

    c.AddCommand<ExecuteDefaultCommand>("default");
    c.AddCommand<ExecuteCommand>("execute");
});

var store = new ConcurrentBucketStore<string>();
store.Set<IBlob>("blob", new MemoryBlob { Data = [1, 2, 3, 4, 5] });
store.Set<IImageBlob>("image", new MemoryImageBlob {
    Data = [2, 3, 4, 5, 6],
    Width = 1920,
    Height = 1080,
    Channels = 4
});
store.Set<IAudioBlob>("audio", new MemoryAudioBlob {
    Data = [3, 4, 5, 6, 7],
    Channels = 2,
    SampleRate = 44100
});

AnsiConsole.WriteLine("Implicit Casting:");
var table = new Table().AddColumns(["Base Type", "Blob", "Image", "Audio"])
    .AddRow(["IBlob", $"{store.Has<IBlob>("blob")}", $"{store.Has<IBlob>("image")}", $"{store.Has<IBlob>("audio")}"])
    .AddRow(["IImageBlob", $"{store.Has<IImageBlob>("blob")}", $"{store.Has<IImageBlob>("image")}", $"{store.Has<IImageBlob>("audio")}"])
    .AddRow(["IAudioBlob", $"{store.Has<IAudioBlob>("blob")}", $"{store.Has<IAudioBlob>("image")}", $"{store.Has<IAudioBlob>("audio")}"]);
AnsiConsole.Write(table);

AnsiConsole.WriteLine("Explicit Casting:");
var table2 = new Table().AddColumns(["Base Type", "Blob", "Image", "Audio"])
    .AddRow(["IBlob", $"{store.CanCast<IBlob>("blob")}", $"{store.CanCast<IBlob>("image")}", $"{store.CanCast<IBlob>("audio")}"])
    .AddRow(["IImageBlob", $"{store.CanCast<IImageBlob>("blob")}", $"{store.CanCast<IImageBlob>("image")}", $"{store.CanCast<IImageBlob>("audio")}"])
    .AddRow(["IAudioBlob", $"{store.CanCast<IAudioBlob>("blob")}", $"{store.CanCast<IAudioBlob>("image")}", $"{store.CanCast<IAudioBlob>("audio")}"]);
AnsiConsole.Write(table2);

await app.RunAsync(args);
