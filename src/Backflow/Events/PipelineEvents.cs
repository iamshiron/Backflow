namespace Shiron.Backflow.Events;

/// <summary>Fired once before the first layer executes.</summary>
/// <param name="TotalLayers">Number of topological layers in the pipeline.</param>
/// <param name="TotalNodes">Total node count across all layers.</param>
public readonly record struct PipelineStartEvent(int TotalLayers, int TotalNodes);

/// <summary>Fired once after the pipeline finishes successfully.</summary>
/// <param name="TotalLayers">Number of topological layers in the pipeline.</param>
/// <param name="TotalNodes">Total node count across all layers.</param>
/// <param name="Stats">Aggregate execution statistics for the run.</param>
public readonly record struct PipelineCompleteEvent(int TotalLayers, int TotalNodes, ExecutionStats Stats);

/// <summary>Fired when a layer begins executing.</summary>
/// <param name="LayerIndex">Zero-based index of the layer within the topological order.</param>
/// <param name="NodeCount">Number of nodes in this layer.</param>
/// <param name="Nodes">The node instances that make up this layer, in topological order.</param>
public readonly record struct PipelineLayerStartEvent(
    int LayerIndex,
    int NodeCount,
    IReadOnlyList<PipelineBuilder.NodeInstance> Nodes
);

/// <summary>Fired when a layer has finished executing all of its nodes.</summary>
/// <param name="LayerIndex">Zero-based index of the layer within the topological order.</param>
/// <param name="NodeCount">Number of nodes that were in this layer.</param>
/// <param name="Elapsed">Wall-clock time spent on this layer.</param>
public readonly record struct PipelineLayerCompleteEvent(int LayerIndex, int NodeCount, TimeSpan Elapsed);

/// <summary>Fired immediately before a node's core logic runs (after skip and cache-hit checks).</summary>
/// <param name="Node">The node instance about to execute.</param>
/// <param name="LayerIndex">Zero-based index of the layer that owns this node.</param>
public readonly record struct PipelineNodeStartEvent(PipelineBuilder.NodeInstance Node, int LayerIndex);

/// <summary>Fired when a node is skipped due to upstream skip propagation.</summary>
/// <param name="Node">The node instance that was skipped.</param>
/// <param name="LayerIndex">Zero-based index of the layer that owns this node.</param>
public readonly record struct PipelineNodeSkipEvent(PipelineBuilder.NodeInstance Node, int LayerIndex);

/// <summary>Fired when a node completes successfully.</summary>
/// <param name="Node">The node instance that completed.</param>
/// <param name="LayerIndex">Zero-based index of the layer that owns this node.</param>
/// <param name="Elapsed">Wall-clock time spent executing the node's core logic (excludes cache writes).</param>
/// <param name="FromCache">True if the outputs were restored from cache instead of executing.</param>
public readonly record struct PipelineNodeSuccessEvent(
    PipelineBuilder.NodeInstance Node,
    int LayerIndex,
    TimeSpan Elapsed,
    bool FromCache
);

/// <summary>Fired when a node fails (throws or returns a failed state).</summary>
/// <param name="Node">The node instance that failed.</param>
/// <param name="LayerIndex">Zero-based index of the layer that owns this node.</param>
/// <param name="Exception">The exception that caused the failure (unwrapped from <see cref="Exceptions.NodeExecutionException"/> when possible).</param>
/// <param name="Elapsed">Wall-clock time spent before the failure.</param>
public readonly record struct PipelineNodeFailureEvent(
    PipelineBuilder.NodeInstance Node,
    int LayerIndex,
    Exception Exception,
    TimeSpan Elapsed
);
