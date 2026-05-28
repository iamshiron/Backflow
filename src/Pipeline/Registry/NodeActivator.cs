using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline.Registry;

public interface INodeActivator : IDisposable {
    TNode CreateNode<TNode>() where TNode : AbstractNode;
    AbstractNode CreateNode(Type type);
}

public sealed class DefaultNodeActivator : INodeActivator {
    private readonly List<AbstractNode> _createdNodes = [];
    private bool _disposed;

    public TNode CreateNode<TNode>() where TNode : AbstractNode {
        var node = Activator.CreateInstance<TNode>();
        lock (_createdNodes) {
            _createdNodes.Add(node);
        }
        return node;
    }

    public AbstractNode CreateNode(Type type) {
        var instance = Activator.CreateInstance(type) ??
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
