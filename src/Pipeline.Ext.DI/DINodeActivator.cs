using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Pipeline.Ext.DI;

public sealed class DINodeActivator(IServiceProvider services) : INodeActivator {
    public TNode CreateNode<TNode>() where TNode : AbstractNode {
        return ActivatorUtilities.CreateInstance<TNode>(services);
    }
    public AbstractNode CreateNode(Type type) {
        var instance = ActivatorUtilities.CreateInstance(services, type) ??
            throw new ArgumentException("Could not create node of type " + type.FullName, "type");
        if (instance is not AbstractNode node)
            throw new ArgumentException("Could not create node of type " + type.FullName, "type");
        return node;
    }
}
