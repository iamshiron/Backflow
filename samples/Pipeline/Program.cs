using Shiron.Lib.Samples.Pipeline.Commands;
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
