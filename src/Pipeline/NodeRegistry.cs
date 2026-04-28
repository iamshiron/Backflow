namespace Shiron.Lib.Pipeline;

public class NodeRegistry {
    private readonly Dictionary<Type, AbstractNode> _nodes = [];

    public void Register(AbstractNode node) {
        _nodes.Add(node.GetType(), node);
    }
    public T Register<T>() where T : AbstractNode {
        var node = Activator.CreateInstance<T>();
        Register(node);
        return node;
    }

    public AbstractNode? Get(Type type) {
        return _nodes.GetValueOrDefault(type);
    }
    public AbstractNode? Get<T>() where T : AbstractNode {
        return Get(typeof(T));
    }
    public AbstractNode? GetByFullName(string fullName) {
        foreach (var kvp in _nodes) {
            if (kvp.Key.FullName == fullName)
                return kvp.Value;
        }
        return null;
    }
}
