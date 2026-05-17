namespace Shiron.Lib.Pipeline.Config;

/// <summary>
/// Build-time configuration for <see cref="PipelineBuilder"/>.
/// </summary>
public record PipelineBuilderConfig {
    /// <summary>When <c>true</c>, reject lossy type casts during <see cref="PipelineBuilder.AddConnection"/>.</summary>
    public bool StrictTypeCasting { get; init; }
    /// <summary>When <c>true</c>, enable per-node output caching during execution.</summary>
    public bool EnableCaching { get; init; }
}
