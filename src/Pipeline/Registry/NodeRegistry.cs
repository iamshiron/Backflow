using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline.Registry;

/// <summary>
/// Registry of pipeline nodes.
/// </summary>
/// <param name="activator">The activator used to create nodes.</param>
public class NodeRegistry(INodeActivator? activator = null) {
    private readonly INodeActivator _activator = activator ?? new DefaultNodeActivator();
    private readonly Dictionary<Type, AbstractNode> _nodes = [];
    private readonly Dictionary<string, AbstractNode> _nodesByFullName = [];
    private readonly HashSet<Type> _nodeTypes = [];
    private readonly Dictionary<string, NodeBlueprint> _blueprints = [];
    private readonly Dictionary<Type, AbstractNode> _concreteGenericCache = [];
    private readonly Dictionary<Type, string[]> _nodeCategories = [];

    /// <summary>Register an already-instantiated node by its runtime type.</summary>
    public void Register(AbstractNode node, params string[]? categories) {
        var type = node.GetType();
        _nodes.Add(type, node);
        _nodesByFullName[type.FullName!] = node;
        _nodeCategories[type] = categories ?? [];
    }

    /// <summary>Create, register, and return a node of type <typeparamref name="T"/>.</summary>
    public T Register<T>(params string[]? categories) where T : AbstractNode {
        var node = _activator.CreateNode<T>();
        Register(node, categories);
        return node;
    }

    /// <summary>
    /// Register an open generic node type and return its <see cref="NodeBlueprint"/>.
    /// The type must derive from <see cref="AbstractGenericNode"/>.
    /// </summary>
    public NodeBlueprint RegisterGeneric(Type type, params string[]? categories) {
        if (!type.IsAssignableTo(typeof(AbstractGenericNode)))
            throw new ArgumentException($"Type must be assignable to {nameof(AbstractGenericNode)}.", nameof(type));

        if (!_nodeTypes.Add(type))
            throw new InvalidOperationException($"Node type {type} already registered.");

        var blueprint = BlueprintFactory.FromOpenType(type);
        _blueprints[blueprint.OpenType.FullName!] = blueprint;
        _nodeCategories[type] = categories ?? [];
        return blueprint;
    }

    public AbstractNode? Get(Type type) {
        return _nodes.GetValueOrDefault(type);
    }

    public T? Get<T>() where T : AbstractNode {
        return (T?) Get(typeof(T));
    }

    public AbstractNode? GetByFullName(string fullName) {
        return _nodesByFullName.GetValueOrDefault(fullName);
    }

    public NodeBlueprint? GetBlueprint(string fullName) {
        return _blueprints.GetValueOrDefault(fullName);
    }

    public NodeBlueprint? GetBlueprint(Type openType) {
        var fullName = openType.FullName;
        return fullName is not null ? _blueprints.GetValueOrDefault(fullName) : null;
    }

    public string[]? GetNodeCategories(Type nodeType) {
        return _nodeCategories.GetValueOrDefault(nodeType);
    }

    /// <summary>Return all registered generic blueprints.</summary>
    public IEnumerable<NodeBlueprint> GetAllBlueprints() {
        return _blueprints.Values;
    }

    /// <summary>
    /// Get or create a closed generic node instance. Results are cached so the same
    /// <paramref name="openType"/> + <paramref name="typeArgs"/> always returns the same instance.
    /// </summary>
    public AbstractNode GetOrCreateConcrete(Type openType, Type[] typeArgs) {
        var closedType = openType.MakeGenericType(typeArgs);
        if (_concreteGenericCache.TryGetValue(closedType, out var cached))
            return cached;

        if (_nodes.TryGetValue(closedType, out var existing))
            return existing;

        var node = _activator.CreateNode(closedType);
        _concreteGenericCache[closedType] = node;
        return node;
    }
}
