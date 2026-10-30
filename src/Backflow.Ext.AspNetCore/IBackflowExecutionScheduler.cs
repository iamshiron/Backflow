using Shiron.Backflow.Context;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>A validated, request-local pipeline and input context ready for host scheduling.</summary>
/// <remarks>The host owns durable admission, execution lifetime, quotas, status and results. Never retain request-scoped services.</remarks>
public sealed record BackflowExecutionRequest(BackflowGraph Graph, Pipeline Pipeline, ArrayPipelineContext Context);

/// <summary>Host-owned asynchronous job admission; the adapter never executes pipelines directly.</summary>
public interface IBackflowExecutionScheduler {
    /// <summary>Accept work or throw a host-handled admission error. Request cancellation applies to admission only.</summary>
    ValueTask<BackflowExecutionAccepted> ScheduleAsync(BackflowExecutionRequest request, CancellationToken cancellationToken);
}
