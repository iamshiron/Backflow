using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Node.Behvaior;

/// <summary>
/// Behavior that adds a <c>ChipEnable</c> boolean input port. When <c>false</c>, the node's
/// core logic is skipped and the node reports <see cref="NodeState.Skipped"/>.
/// </summary>
public class ChipEnableBehavior : INodeBehavior {
    /// <summary>The chip-enable control port. Defaults to <c>true</c>.</summary>
    public IInputPort<bool> ChipEnable { get; private set; } = null!;

    public void AttachPorts(AbstractNode node) {
        ChipEnable = node.Input(
            new BoolPortBuilder(nameof(ChipEnable))
                .Default(true)
                .Input()
        );
    }
    public ValueTask<(bool shouldContinue, bool result)> PreExecuteAsync(INodeContext context) {
        return new ValueTask<(bool shouldContinue, bool result)>(
            (ChipEnable.Read(context), true)
        );
    }
    public ValueTask PostExecuteAsync(INodeContext context, bool result) {
        return default;
    }
}
