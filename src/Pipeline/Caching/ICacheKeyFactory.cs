using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Creates <see cref="ICacheKey"/> instances from a node instance and its current context.
/// </summary>
public interface ICacheKeyFactory {
    /// <summary>Build a cache key for the given node, hashing its input values.</summary>
    ICacheKey CreateKey(PipelineBuilder.NodeInstance node, IPipelineContext context);
}
