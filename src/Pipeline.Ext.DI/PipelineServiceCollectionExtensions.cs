using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Pipeline.Ext.DI;

public static class PipelineServiceCollectionExtensions {
    public static IServiceCollection AddPipelineEngine(this IServiceCollection services) {
        services.AddSingleton<INodeActivator, DINodeActivator>();
        return services;
    }
}
