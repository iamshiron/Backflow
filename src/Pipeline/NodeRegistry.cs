using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Central registry for node types and generic blueprints. Used by <see cref="PipelineBuilder"/>
/// to look up and materialize nodes.
/// </summary>
public class NodeRegistry {
    private readonly Dictionary<Type, AbstractNode> _nodes = [];
    private readonly Dictionary<string, AbstractNode> _nodesByFullName = [];
    private readonly HashSet<Type> _nodeTypes = [];
    private readonly Dictionary<string, NodeBlueprint> _blueprints = [];
    private readonly Dictionary<Type, AbstractNode> _concreteGenericCache = [];

    /// <summary>Register an already-instantiated node by its runtime type.</summary>
    public void Register(AbstractNode node) {
        _nodes.Add(node.GetType(), node);
        _nodesByFullName[node.GetType().FullName!] = node;
    }

    /// <summary>Create, register, and return a node of type <typeparamref name="T"/>.</summary>
    public T Register<T>() where T : AbstractNode {
        var node = Activator.CreateInstance<T>();
        Register(node);
        return node;
    }

    /// <summary>
    /// Register an open generic node type and return its <see cref="NodeBlueprint"/>.
    /// The type must derive from <see cref="AbstractGenericNode"/>.
    /// </summary>
    public NodeBlueprint RegisterGeneric(Type type) {
        if (!type.IsAssignableTo(typeof(AbstractGenericNode)))
            throw new ArgumentException($"Type must be assignable to {nameof(AbstractGenericNode)}.", nameof(type));

        if (!_nodeTypes.Add(type))
            throw new InvalidOperationException($"Node type {type} already registered.");

        var blueprint = BlueprintFactory.FromOpenType(type);
        _blueprints[blueprint.OpenType.FullName!] = blueprint;
        return blueprint;
    }

    /// <summary>Look up a registered node by its runtime type.</summary>
    public AbstractNode? Get(Type type) {
        return _nodes.GetValueOrDefault(type);
    }

    /// <summary>Look up a registered node by its runtime type.</summary>
    public T? Get<T>() where T : AbstractNode {
        return (T?) Get(typeof(T));
    }

    /// <summary>Look up a registered node by its fully-qualified type name.</summary>
    public AbstractNode? GetByFullName(string fullName) {
        return _nodesByFullName.GetValueOrDefault(fullName);
    }

    /// <summary>Look up a generic blueprint by the open type's fully-qualified name.</summary>
    public NodeBlueprint? GetBlueprint(string fullName) {
        return _blueprints.GetValueOrDefault(fullName);
    }

    /// <summary>Look up a generic blueprint by the open type.</summary>
    public NodeBlueprint? GetBlueprint(Type openType) {
        var fullName = openType.FullName;
        return fullName is not null ? _blueprints.GetValueOrDefault(fullName) : null;
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

        var node = (AbstractNode) Activator.CreateInstance(closedType)!;
        _concreteGenericCache[closedType] = node;
        return node;
    }
}
