using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Caching;

public interface ICacheKeyFactory {
    ICacheKey CreateKey(PipelineBuilder.NodeInstance node, IPipelineContext context);
}
