using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Ext.DI;
using Shiron.Lib.Pipeline.Registry;
using Shiron.Lib.Pipeline.Serialization;
using Shiron.Lib.Samples.Pipeline.Serialization;
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

        [CommandOption("--cache")]
        [Description("Enable caching")]
        public bool EnableCaching { get; init; }
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
                ? PipelineSerialization.DeserializeInputs(await File.ReadAllTextAsync(settings.InputsFile, cancellationToken), pipeline)
                : new PipelineContext();

            CacheTypeAdapterRegistry? adapters = null;
            if (settings.EnableCaching) {
                adapters = new CacheTypeAdapterRegistry();
                adapters.FromAttributes();

                Console.WriteLine($"Registered Adapters: {string.Join(", ", adapters.Converters.Select(c => c.GetType().FullName!))}");
            }

            using var cache = settings.EnableCaching ? new JsonFileCache(".output/cache.json", adapters) : null;
            var blobStorage = settings.EnableCaching ? new GlobalStorageRegistry(".output") : null;

            var executor = new PipelineExecutor(pipeline, cache, typeAdapters: adapters, blobResolver: blobStorage);
            var stats = await executor.ExecuteAsync(context);
            if (cache is not null) await cache.FlushAsync();
            AnsiConsole.MarkupLine($"[green]{stats}[/]");
        } catch (Exception e) {
            AnsiConsole.WriteException(e);
        }
        return 0;
    }
}
