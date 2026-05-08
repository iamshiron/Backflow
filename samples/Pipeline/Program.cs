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

await app.RunAsync(args);
