using Shiron.Backflow;

namespace Shiron.Backflow.Events;

/// <summary>
/// Observes pipeline execution lifecycle events for progress reporting, structured logging, and metrics
/// without touching node code. Every method has a default no-op implementation — override only the events
/// you care about.
/// </summary>
/// <remarks>
/// Implementations MUST be thread-safe when used with
/// <see cref="PipelineExecutor.ExecuteAsync"/>: nodes within a single layer run in parallel, so
/// <see cref="OnNodeStart"/>, <see cref="OnNodeSkip"/>, <see cref="OnNodeSuccess"/>, and
/// <see cref="OnNodeFailure"/> may fire concurrently. Layer-level events are always sequential.
/// </remarks>
public interface IPipelineEventListener {
    /// <summary>Fired once before the first layer executes.</summary>
    void OnPipelineStart(in PipelineStartEvent e) { }

    /// <summary>Fired once after the pipeline finishes successfully.</summary>
    void OnPipelineComplete(in PipelineCompleteEvent e) { }

    /// <summary>Fired when a layer begins executing.</summary>
    void OnLayerStart(in PipelineLayerStartEvent e) { }

    /// <summary>Fired when a layer has finished executing all of its nodes.</summary>
    void OnLayerComplete(in PipelineLayerCompleteEvent e) { }

    /// <summary>Fired immediately before a node's core logic runs (after skip and cache-hit checks).</summary>
    void OnNodeStart(in PipelineNodeStartEvent e) { }

    /// <summary>Fired when a node is skipped due to upstream skip propagation.</summary>
    void OnNodeSkip(in PipelineNodeSkipEvent e) { }

    /// <summary>Fired when a node completes successfully (executed or restored from cache).</summary>
    void OnNodeSuccess(in PipelineNodeSuccessEvent e) { }

    /// <summary>Fired when a node fails (throws or returns a failed state).</summary>
    void OnNodeFailure(in PipelineNodeFailureEvent e) { }
}
