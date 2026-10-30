using Shiron.Backflow.Context;

namespace Shiron.Backflow.Caching;

/// <summary>Application-controlled semantic identity for cache keys.</summary>
public sealed class CacheKeyFactoryOptions {
    /// <summary>Explicit application/cache algorithm revision, independent of assembly version.</summary>
    public string Version { get; init; } = "1";
    /// <summary>Additional semantic node settings or context dependencies; must return a deterministic snapshot.</summary>
    public Func<PipelineBuilder.NodeInstance, IPipelineContext, object?>? IdentitySelector { get; init; }
}
