using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiron.Backflow.Ext.DI;
using Shiron.Backflow.Registry;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>ASP.NET Core registration for a host-owned Backflow node catalog.</summary>
public static class BackflowServiceCollectionExtensions {
    /// <summary>Register the thin adapter and publish only explicitly selected registry nodes.</summary>
    /// <remarks>Register an existing NodeRegistry first to reuse it. Node activation uses the existing DI extension.</remarks>
    public static IServiceCollection AddBackflow(this IServiceCollection services,
        Action<IServiceProvider, BackflowCatalogBuilder> configureCatalog, Action<BackflowOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureCatalog);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(BackflowGraphService)))
            throw new InvalidOperationException("Backflow has already been registered.");
        var options = new BackflowOptions();
        configure?.Invoke(options);
        options.Validate();
        services.AddSingleton(options);
        services.TryAddSingleton<INodeActivator, DINodeActivator>();
        services.TryAddSingleton<NodeRegistry>();
        services.AddSingleton(provider => {
            var builder = new BackflowCatalogBuilder(provider.GetRequiredService<NodeRegistry>());
            configureCatalog(provider, builder);
            return new BackflowGraphService(builder, options);
        });
        return services;
    }
}
