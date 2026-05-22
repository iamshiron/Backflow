using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Ext.DI;

public readonly struct ScopedPipelineContext : IAsyncDisposable {
    public PipelineContext Context { get; }
    private readonly AsyncServiceScope _scope;

    public ScopedPipelineContext(PipelineContext context, AsyncServiceScope scope) {
        Context = context;
        _scope = scope;
    }

    public ValueTask DisposeAsync() {
        return _scope.DisposeAsync();
    }
}

public static class PipelineContextExtensions {
    /// <summary>
    /// Creates a DI scope and a new PipelineContext bound to that scope.
    /// Dispose this struct to clean up scoped dependencies.
    /// </summary>
    public static ScopedPipelineContext CreateScopedContext(this IServiceProvider provider) {
        var scope = provider.CreateAsyncScope();
        var context = new PipelineContext(scope.ServiceProvider);
        return new ScopedPipelineContext(context, scope);
    }
}
