namespace Shiron.Lib.Pipeline.Node;

public abstract class AbstractGenericNode : AbstractNode {
    public Type[] GetTypeArguments() => GetType().GetGenericArguments();
}
