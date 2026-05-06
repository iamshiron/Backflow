using System.ComponentModel;
using System.Text.Json;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shiron.Lib.Samples.Pipeline.Commands;

public class ExecuteCommand : AsyncCommand<ExecuteCommand.Settings> {
    public sealed class Settings : CommandSettings {
        [CommandArgument(0, "<file>")]
        [Description("The file of the pipeline definition to execute")]
        public string File { get; init; } = string.Empty;
    }

    protected async override Task<int> ExecuteAsync(CommandContext cmdContext, Settings settings, CancellationToken cancellationToken) {
        try {
            var registry = new GlobalNodeRegistry();
            var (pipeline, context) =
                PipelineSerialization.DeserializePipeline(await File.ReadAllTextAsync(settings.File, cancellationToken), registry.Registry);

            var executor = new PipelineExecutor(pipeline);
            await executor.ExecuteAsync(context);
        } catch (Exception e) {
            AnsiConsole.WriteException(e);
        }
        return 0;
    }
}
