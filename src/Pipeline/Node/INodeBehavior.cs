using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Node;

public interface INodeBehavior {
    void AttachPorts(AbstractNode node);
    ValueTask<(bool shouldContinue, bool result)> PreExecuteAsync(INodeContext context);
    ValueTask PostExecuteAsync(INodeContext context, bool result);
}
