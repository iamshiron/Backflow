using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Ext.DI;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline.Ext.DI;

public class PipelineContextExtensionsTests {
    private class ScopedService { }

    [Fact]
    public async Task CreateScopedContext_ReturnsNonNullContext() {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        await using var scoped = provider.CreateScopedContext();

        Assert.NotNull(scoped.Context);
    }

    [Fact]
    public async Task CreateScopedContext_ContextHasScopedServiceProvider() {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        await using var scoped = provider.CreateScopedContext();

        Assert.NotNull(scoped.Context.Services);
        Assert.NotSame(provider, scoped.Context.Services);
    }

    [Fact]
    public async Task CreateScopedContext_SeparateScopesHaveDifferentServiceProviders() {
        var services = new ServiceCollection();
        services.AddScoped<ScopedService>();
        var provider = services.BuildServiceProvider();

        await using var scoped1 = provider.CreateScopedContext();
        await using var scoped2 = provider.CreateScopedContext();

        var svc1 = scoped1.Context.Services.GetRequiredService<ScopedService>();
        var svc2 = scoped2.Context.Services.GetRequiredService<ScopedService>();

        Assert.NotSame(svc1, svc2);
    }

    [Fact]
    public async Task CreateScopedContext_SameScopeResolvesSameScopedService() {
        var services = new ServiceCollection();
        services.AddScoped<ScopedService>();
        var provider = services.BuildServiceProvider();

        await using var scoped = provider.CreateScopedContext();

        var svc1 = scoped.Context.Services.GetRequiredService<ScopedService>();
        var svc2 = scoped.Context.Services.GetRequiredService<ScopedService>();

        Assert.Same(svc1, svc2);
    }

    [Fact]
    public async Task DisposeAsync_CompletesWithoutError() {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var scoped = provider.CreateScopedContext();
        await scoped.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DoubleDisposeDoesNotThrow() {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var scoped = provider.CreateScopedContext();
        await scoped.DisposeAsync();
        await scoped.DisposeAsync();
    }
}
