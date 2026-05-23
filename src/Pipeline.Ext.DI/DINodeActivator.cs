using Microsoft.Extensions.DependencyInjection;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Pipeline.Ext.DI;

public sealed class DINodeActivator(IServiceProvider services) : INodeActivator, IDisposable {
    private readonly List<AbstractNode> _createdNodes = [];
    private bool _disposed;

    public TNode CreateNode<TNode>() where TNode : AbstractNode {
        var node = ActivatorUtilities.CreateInstance<TNode>(services);
        lock (_createdNodes) {
            _createdNodes.Add(node);
        }
        return node;
    }

    public AbstractNode CreateNode(Type type) {
        var instance = ActivatorUtilities.CreateInstance(services, type) ??
            throw new ArgumentException("Could not create node of type " + type.FullName, "type");
        if (instance is not AbstractNode node)
            throw new ArgumentException("Could not create node of type " + type.FullName, "type");
        lock (_createdNodes) {
            _createdNodes.Add(node);
        }
        return node;
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        lock (_createdNodes) {
            foreach (var node in _createdNodes) {
                (node as IDisposable)?.Dispose();
            }
            _createdNodes.Clear();
        }
    }
}
