using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline.Registry;

public interface INodeActivator {
    TNode CreateNode<TNode>() where TNode : AbstractNode;
    AbstractNode CreateNode(Type type);
}

public sealed class DefaultNodeActivator : INodeActivator {
    public TNode CreateNode<TNode>() where TNode : AbstractNode {
        return Activator.CreateInstance<TNode>();
    }
    public AbstractNode CreateNode(Type type) {
        var instance = Activator.CreateInstance(type) ??
            throw new ArgumentException("Could not create node of type " + type.FullName, "type");
        if (instance is not AbstractNode node)
            throw new ArgumentException("Could not create node of type " + type.FullName, "type");
        return node;
    }
}
