namespace Shiron.Lib.Pipeline;

/// <summary>
/// Execution statistics returned by <see cref="PipelineExecutor.ExecuteAsync"/>.
/// </summary>
/// <param name="TotalNodes">Total number of nodes in the pipeline.</param>
/// <param name="ExecutedNodes">Nodes that actually ran their <c>ExecuteNodeAsync</c> method.</param>
/// <param name="SkippedNodes">Nodes whose execution was skipped.</param>
/// <param name="CacheHits">Nodes whose outputs were restored from cache.</param>
/// <param name="CacheMisses">Cacheable nodes that were not found in cache and had to execute.</param>
/// <param name="TotalTime">Wall-clock time for the entire pipeline execution.</param>
public readonly record struct ExecutionStats(
    int TotalNodes,
    int ExecutedNodes,
    int SkippedNodes,
    int CacheHits,
    int CacheMisses,
    TimeSpan TotalTime
);
