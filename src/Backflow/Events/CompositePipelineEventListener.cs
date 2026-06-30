namespace Shiron.Backflow.Events;

/// <summary>
/// Aggregates multiple <see cref="IPipelineEventListener"/> instances and fans every event out to all of them,
/// in registration order.
/// </summary>
public sealed class CompositePipelineEventListener(params IPipelineEventListener[] listeners) : IPipelineEventListener {
    private readonly IPipelineEventListener[] _listeners = listeners;

    /// <inheritdoc/>
    public void OnPipelineStart(in PipelineStartEvent e) {
        foreach (var l in _listeners) l.OnPipelineStart(in e);
    }

    /// <inheritdoc/>
    public void OnPipelineComplete(in PipelineCompleteEvent e) {
        foreach (var l in _listeners) l.OnPipelineComplete(in e);
    }

    /// <inheritdoc/>
    public void OnLayerStart(in PipelineLayerStartEvent e) {
        foreach (var l in _listeners) l.OnLayerStart(in e);
    }

    /// <inheritdoc/>
    public void OnLayerComplete(in PipelineLayerCompleteEvent e) {
        foreach (var l in _listeners) l.OnLayerComplete(in e);
    }

    /// <inheritdoc/>
    public void OnNodeStart(in PipelineNodeStartEvent e) {
        foreach (var l in _listeners) l.OnNodeStart(in e);
    }

    /// <inheritdoc/>
    public void OnNodeSkip(in PipelineNodeSkipEvent e) {
        foreach (var l in _listeners) l.OnNodeSkip(in e);
    }

    /// <inheritdoc/>
    public void OnNodeSuccess(in PipelineNodeSuccessEvent e) {
        foreach (var l in _listeners) l.OnNodeSuccess(in e);
    }

    /// <inheritdoc/>
    public void OnNodeFailure(in PipelineNodeFailureEvent e) {
        foreach (var l in _listeners) l.OnNodeFailure(in e);
    }
}
