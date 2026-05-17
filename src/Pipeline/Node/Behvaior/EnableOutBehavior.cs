using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Node.Behvaior;

/// <summary>
/// Behavior that adds an <c>EnableOut</c> boolean output port. After execution,
/// writes <c>true</c> on success or <c>false</c> on failure.
/// </summary>
public class EnableOutBehavior : INodeBehavior {
    /// <summary>The enable-out status port. Defaults to <c>false</c>.</summary>
    public IOutputPort<bool> EnableOut { get; private set; } = null!;

    public void AttachPorts(AbstractNode node) {
        EnableOut = node.Output(
            new BoolPortBuilder(nameof(EnableOut))
                .Default(false)
                .Output()
        );
    }
    public ValueTask<(bool shouldContinue, bool result)> PreExecuteAsync(INodeContext context) {
        return new ValueTask<(bool shouldContinue, bool result)>((true, true));
    }
    public ValueTask PostExecuteAsync(INodeContext context, bool result) {
        EnableOut.Write(context, result);
        return default;
    }
}
