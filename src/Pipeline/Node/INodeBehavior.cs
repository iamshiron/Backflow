using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Node;

/// <summary>
/// Lifecycle hook that wraps node execution. Implementations add extra ports
/// (e.g., ChipEnable, EnableOut) and control whether the core logic runs.
/// </summary>
public interface INodeBehavior {
    /// <summary>Called once at registration time to declare additional ports on <paramref name="node"/>.</summary>
    void AttachPorts(AbstractNode node);
    /// <summary>
    /// Called before the node's <c>ExecuteNodeAsync</c>. Return <c>(false, true)</c> to skip core execution
    /// while reporting success, or <c>(*, false)</c> to fail the node immediately.
    /// </summary>
    ValueTask<(bool shouldContinue, bool result)> PreExecuteAsync(INodeContext context);
    /// <summary>Called after core execution with the success/failure result.</summary>
    ValueTask PostExecuteAsync(INodeContext context, bool result);
}
