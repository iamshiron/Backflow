using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Ext.DI;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline.Ext.DI;

public class PipelineServiceCollectionExtensionsTests {
    [Fact]
    public void AddPipelineEngine_RegistersDINodeActivatorAsINodeActivator() {
        var services = new ServiceCollection();
        services.AddPipelineEngine();
        var provider = services.BuildServiceProvider();

        var activator = provider.GetService<INodeActivator>();

        Assert.NotNull(activator);
        Assert.IsType<DINodeActivator>(activator);
    }

    [Fact]
    public void AddPipelineEngine_ReturnsSameServiceCollection() {
        var services = new ServiceCollection();

        var result = services.AddPipelineEngine();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddPipelineEngine_RegistersAsSingleton() {
        var services = new ServiceCollection();
        services.AddPipelineEngine();
        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<INodeActivator>();
        var second = provider.GetRequiredService<INodeActivator>();

        Assert.Same(first, second);
    }

    private class TestRegistry { }

    [Fact]
    public void AddGlobalNodeRegistry_RegistersRegistryAsSingleton() {
        var services = new ServiceCollection();
        services.AddGlobalNodeRegistry<TestRegistry>();
        var provider = services.BuildServiceProvider();

        var registry = provider.GetService<TestRegistry>();

        Assert.NotNull(registry);
        Assert.IsType<TestRegistry>(registry);
    }

    [Fact]
    public void AddGlobalNodeRegistry_ReturnsSameServiceCollection() {
        var services = new ServiceCollection();

        var result = services.AddGlobalNodeRegistry<TestRegistry>();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddGlobalNodeRegistry_ResolvesSameInstanceTwice() {
        var services = new ServiceCollection();
        services.AddGlobalNodeRegistry<TestRegistry>();
        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<TestRegistry>();
        var second = provider.GetRequiredService<TestRegistry>();

        Assert.Same(first, second);
    }
}
