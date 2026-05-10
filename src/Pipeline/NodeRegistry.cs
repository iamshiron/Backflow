using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline;

public class NodeRegistry {
    private readonly Dictionary<Type, AbstractNode> _nodes = [];
    private readonly Dictionary<string, AbstractNode> _nodesByFullName = [];
    private readonly HashSet<Type> _nodeTypes = [];
    private readonly Dictionary<string, NodeBlueprint> _blueprints = [];
    private readonly Dictionary<Type, AbstractNode> _concreteGenericCache = [];

    public void Register(AbstractNode node) {
        _nodes.Add(node.GetType(), node);
        _nodesByFullName[node.GetType().FullName!] = node;
    }

    public T Register<T>() where T : AbstractNode {
        var node = Activator.CreateInstance<T>();
        Register(node);
        return node;
    }

    public NodeBlueprint RegisterGeneric(Type type) {
        if (!type.IsAssignableTo(typeof(AbstractGenericNode)))
            throw new ArgumentException($"Type must be assignable to {nameof(AbstractGenericNode)}.", nameof(type));

        if (!_nodeTypes.Add(type))
            throw new InvalidOperationException($"Node type {type} already registered.");

        var blueprint = BlueprintFactory.FromOpenType(type);
        _blueprints[blueprint.OpenType.FullName!] = blueprint;
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

    public IEnumerable<NodeBlueprint> GetAllBlueprints() {
        return _blueprints.Values;
    }

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
