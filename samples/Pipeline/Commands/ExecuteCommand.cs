using System.ComponentModel;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Serialization;
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
            var registry = new GlobalNodeRegistry();

            var pipeline =
                PipelineSerialization.DeserializeDefinition(await File.ReadAllTextAsync(settings.File, cancellationToken), registry.Registry);

            var context = settings.InputsFile is not null
                ? PipelineSerialization.DeserializeInputs(await File.ReadAllTextAsync(settings.InputsFile, cancellationToken), pipeline)
                : new PipelineContext();

            var executor = new PipelineExecutor(pipeline);
            await executor.ExecuteAsync(context);
        } catch (Exception e) {
            AnsiConsole.WriteException(e);
        }
        return 0;
    }
}
