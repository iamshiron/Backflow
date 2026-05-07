using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Node.Behvaior;

public class EnableOutBehavior : INodeBehavior {
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
