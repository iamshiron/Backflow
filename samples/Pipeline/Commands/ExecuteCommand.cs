using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Ext.DI;
using Shiron.Lib.Pipeline.Registry;
using Shiron.Lib.Pipeline.Serialization;
using Shiron.Lib.Samples.Pipeline.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Lib.Samples.Pipeline.Commands;

public class ExecuteCommand : AsyncCommand<ExecuteCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<file>")]
        [Description("The pipeline definition file to execute")]
        public string File { get; init; } = string.Empty;

        [CommandOption("--inputs")]
        [Description("Optional pipeline inputs file")]
        public string? InputsFile { get; init; }
    }

    protected async override Task<int> ExecuteAsync(CommandContext cmdContext, Settings settings, CancellationToken cancellationToken) {
        try {
            var services = new ServiceCollection();
            services.AddPipelineEngine();
            services.AddSingleton<IPrintService, PrintService>();
            var provider = services.BuildServiceProvider();

            var registry = new GlobalNodeRegistry(provider.GetRequiredService<INodeActivator>());

            var pipeline =
                PipelineSerialization.DeserializeDefinition(await File.ReadAllTextAsync(settings.File, cancellationToken), registry.Registry);

            var context = settings.InputsFile is not null
                ? PipelineSerialization.DeserializeInputs(await File.ReadAllTextAsync(settings.InputsFile, cancellationToken), pipeline, provider)
                : new PipelineContext(provider);

            var executor = new PipelineExecutor(pipeline);
            var stats = await executor.ExecuteAsync(context);
            AnsiConsole.MarkupLine($"[green]{stats}[/]");
        } catch (Exception e) {
            AnsiConsole.WriteException(e);
        }
        return 0;
    }
}
