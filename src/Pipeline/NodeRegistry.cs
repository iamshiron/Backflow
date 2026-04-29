namespace Shiron.Lib.Pipeline;

/// <summary>
/// Named lookup for <see cref="AbstractNode"/> instances by type.
/// Required for deserialization via <see cref="Serialization.PipelineSerialization"/>.
/// </summary>
public class NodeRegistry {
    private readonly Dictionary<Type, AbstractNode> _nodes = [];

    /// <summary>Register an existing node instance.</summary>
    /// <param name="node">Node to register. Keyed by its runtime type.</param>
    public void Register(AbstractNode node) {
        _nodes.Add(node.GetType(), node);
    }

    /// <summary>Create and register a node of type <typeparamref name="T"/>.</summary>
    public T Register<T>() where T : AbstractNode {
        var node = Activator.CreateInstance<T>();
        Register(node);
        return node;
    }

    /// <summary>Lookup by <see cref="Type"/>.</summary>
    /// <param name="type">Exact runtime type of the node.</param>
    public AbstractNode? Get(Type type) {
        return _nodes.GetValueOrDefault(type);
    }

    /// <summary>Lookup by generic type.</summary>
    public T? Get<T>() where T : AbstractNode {
        return (T?) Get(typeof(T));
    }

    /// <summary>Lookup by fully-qualified type name (e.g. <c>"MyNamespace.MyNode"</c>).</summary>
    /// <param name="fullName">Fully qualified type name of the node.</param>
    public AbstractNode? GetByFullName(string fullName) {
        foreach (var kvp in _nodes) {
            if (kvp.Key.FullName == fullName)
                return kvp.Value;
        }
        return null;
    }
}
