namespace Shiron.Lib.Pipeline;

/// <summary>
/// Execution statistics returned by <see cref="PipelineExecutor.Execute"/>
/// and <see cref="PipelineExecutor.ExecuteAsync"/>.
/// </summary>
/// <param name="TotalNodes">Total number of nodes in the pipeline.</param>
/// <param name="ExecutedNodes">Nodes that actually ran their <c>Execute</c> method.</param>
/// <param name="SkippedNodes">Nodes whose execution was skipped due to a cache hit.</param>
/// <param name="CacheHits">Number of successful cache lookups.</param>
/// <param name="CacheMisses">Number of failed cache lookups (node had to execute).</param>
/// <param name="CacheUpdates">Number of entries written (or overwritten) in the cache.</param>
/// <param name="TotalTime">Wall-clock time for the entire pipeline execution.</param>
public readonly record struct ExecutionStats(
    int TotalNodes,
    int ExecutedNodes,
    int SkippedNodes,
    int CacheHits,
    int CacheMisses,
    int CacheUpdates,
    TimeSpan TotalTime
);
