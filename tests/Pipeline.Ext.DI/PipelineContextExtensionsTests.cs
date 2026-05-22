using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Ext.DI;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline.Ext.DI;

public class PipelineContextExtensionsTests {
    private class ScopedService { }

    private interface IRuntimeService { string GetValue(); }
    private sealed class RuntimeService : IRuntimeService {
        public string GetValue() => "from-di";
    }

    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private sealed class ServiceResolvingNode : AbstractNode {
        public IInputPort<string> In { get; }
        public IOutputPort<string> Out { get; }
        public string? ResolvedValue { get; private set; }

        public ServiceResolvingNode() {
            In = Input(new InputPort<string>("in", default, new PassValidator<string>()));
            Out = Output(new OutputPort<string>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var svc = context.Services.GetRequiredService<IRuntimeService>();
            ResolvedValue = svc.GetValue();
            var input = In.Read(context);
            Out.Write(context, $"{input}-{ResolvedValue}");
            return ValueTask.FromResult(true);
        }
    }

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

    [Fact]
    public async Task NodeContext_Services_ResolvesScopedServiceDuringExecution() {
        var services = new ServiceCollection();
        services.AddSingleton<IRuntimeService, RuntimeService>();
        services.AddPipelineEngine();
        var provider = services.BuildServiceProvider();

        await using var scoped = provider.CreateScopedContext();
        var context = scoped.Context;

        var registry = new NodeRegistry(provider.GetRequiredService<INodeActivator>());
        var node = registry.Register<ServiceResolvingNode>();
        var builder = new PipelineBuilder(registry);
        var instance = builder.AddNode(node);

        context.Write(instance, node.In, "test");
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        Assert.Equal("from-di", node.ResolvedValue);
        Assert.Equal("test-from-di", context.Read<string>(instance, node.Out));
    }
}
