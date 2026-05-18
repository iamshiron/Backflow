using System.Reflection;
using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Config;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Pipeline;

public class PipelineBuilder(NodeRegistry registry, CastRegistry? castRegistry = null) {
    public PipelineBuilderConfig Config { get; set; } = new();

    private readonly CastRegistry _castRegistry = castRegistry ?? CastRegistry.CreateDefault();

    public PipelineBuilder RegisterCast<TSrc, TDst>(TypeCast castType, Func<TSrc, TDst> converter) {
        _castRegistry.Register(castType, converter);
        return this;
    }

    public PipelineContext CreateContext() {
        return new PipelineContext(_castRegistry);
    }

    public record class NodeInstance(
        string ID,
        AbstractNode Node,
        Dictionary<IPort, Guid> Mappings
    ) {
        public NodeState State { get; set; } = NodeState.Pending;
        public virtual bool Equals(NodeInstance? other) {
            return other is not null && ID == other.ID;
        }
        public override int GetHashCode() {
            return ID.GetHashCode();
        }
    }

    public readonly record struct EdgeInstance(
        NodeInstance SourceNode, IPort SourcePort,
        NodeInstance DestinationNode, IPort DestinationPort,
        int? DestIndex = null
    );

    private readonly DirectedAcyclicGraph<NodeInstance> _graph = new();
    private readonly List<EdgeInstance> _edges = [];
    private readonly Dictionary<string, int> _nodeTypeCounts = [];
    private readonly Dictionary<string, GenericNodeRef> _genericNodes = [];
    private readonly List<PendingEdge> _pendingEdges = [];
    private readonly Dictionary<string, HashSet<string>> _adjacency = [];

    private string NextId(string fullName) {
        var count = _nodeTypeCounts.GetValueOrDefault(fullName, 0);
        _nodeTypeCounts[fullName] = count + 1;
        return $"{fullName}-{count}";
    }

    public NodeInstance AddNode(AbstractNode node) {
        return AddNode(node, []);
    }

    public NodeInstance AddNode(AbstractNode node, Dictionary<string, int> arrayCounts) {
        var fullName = node.GetType().FullName!;
        var id = NextId(fullName);

        var mappings = new Dictionary<IPort, Guid>();

        foreach (var port in node.Ports) {
            mappings[port] = Guid.NewGuid();

            if (port is IArrayInputPortMarker arrayPort) {
                if (arrayCounts.TryGetValue(port.Name, out var count)) {
                    if (count < arrayPort.MinCount) {
                        throw new ArgumentException(
                            $"Array port '{port.Name}' requires at least {arrayPort.MinCount} elements, got {count}.",
                            nameof(arrayCounts));
                    }
                    if (arrayPort.MaxCount.HasValue && count > arrayPort.MaxCount.Value) {
                        throw new ArgumentException(
                            $"Array port '{port.Name}' supports at most {arrayPort.MaxCount.Value} elements, got {count}.",
                            nameof(arrayCounts));
                    }
                    arrayPort.SetCount(count);
                }
            }
        }

        var instance = new NodeInstance(id, node, mappings);
        _graph.AddNode(instance);
        return instance;
    }

    public NodeInstance AddNode(NodeBlueprint blueprint, Type[] typeArgs) {
        var node = registry.GetOrCreateConcrete(blueprint.OpenType, typeArgs);
        return AddNode(node);
    }

    public GenericNodeRef AddNode(NodeBlueprint blueprint) {
        var fullName = blueprint.OpenType.FullName!;
        var id = NextId(fullName);

        var portMappings = new Dictionary<string, Guid>();
        foreach (var port in blueprint.Ports) {
            portMappings[port.Name] = Guid.NewGuid();
        }

        var genericRef = new GenericNodeRef(id, blueprint, portMappings, registry);
        _genericNodes[id] = genericRef;
        return genericRef;
    }

    public void AddConnection(NodeInstance source, IPort sourcePort, NodeInstance destination, IPort destinationPort) {
        if (!source.Node.Ports.Contains(sourcePort))
            throw new InvalidPortException(sourcePort, source.Node.GetType());

        if (!destination.Node.Ports.Contains(destinationPort))
            throw new InvalidPortException(destinationPort, destination.Node.GetType());

        ValidateTypeCompatibility(sourcePort, destinationPort);

        CheckCycle(source.ID, destination.ID);

        _edges.Add(new EdgeInstance(source, sourcePort, destination, destinationPort));
        destination.Mappings[destinationPort] = source.Mappings[sourcePort];
    }

    public void AddConnection(NodeInstance source, IPort sourcePort, NodeInstance dest, IPort destPort, int destIndex) {
        if (!dest.Node.Ports.Contains(destPort))
            throw new InvalidPortException(destPort, dest.Node.GetType());

        if (destPort is not IArrayInputPortMarker)
            throw new ArgumentException($"Port '{destPort.Name}' is not an array input port.", nameof(destPort));

        if (destPort is IArrayInputPortMarker { IsFrozen: true } frozen) {
            if (destIndex < 0 || destIndex >= frozen.Count!.Value)
                throw new ArgumentOutOfRangeException(nameof(destIndex),
                    $"Index {destIndex} is out of range for array port '{destPort.Name}' (count: {frozen.Count}).");
        }

        CheckCycle(source.ID, dest.ID);

        _edges.Add(new EdgeInstance(source, sourcePort, dest, destPort, destIndex));
    }

    public void AddConnection(NodeInstance source, IPort sourcePort, GenericNodeRef destination, IPort destinationPort) {
        CheckCycle(source.ID, destination.ID);
        _pendingEdges.Add(new PendingEdge(source.ID, sourcePort, destination.ID, destinationPort));

        if (!destination.IsResolved) {
            var sourceType = sourcePort.PortType;
            if (sourceType != typeof(void) && destinationPort is BlueprintPort bp)
                TryResolveTypeArg(destination, bp.TypeParameterIndex, sourceType);
        }
    }

    public void AddConnection(GenericNodeRef source, IPort sourcePort, NodeInstance destination, IPort destinationPort) {
        CheckCycle(source.ID, destination.ID);
        _pendingEdges.Add(new PendingEdge(source.ID, sourcePort, destination.ID, destinationPort));

        if (!source.IsResolved && sourcePort is BlueprintPort bp) {
            var destType = destinationPort.PortType;
            if (destType != typeof(void))
                TryResolveTypeArg(source, bp.TypeParameterIndex, destType);
        }
    }

    public void AddConnection(GenericNodeRef source, IPort sourcePort, GenericNodeRef destination, IPort destinationPort) {
        CheckCycle(source.ID, destination.ID);
        _pendingEdges.Add(new PendingEdge(source.ID, sourcePort, destination.ID, destinationPort));

        if (source.IsResolved && !destination.IsResolved && destinationPort is BlueprintPort destBp) {
            var type = GetResolvedPortType(source, sourcePort);
            if (type != typeof(void))
                TryResolveTypeArg(destination, destBp.TypeParameterIndex, type);
        } else if (destination.IsResolved && !source.IsResolved && sourcePort is BlueprintPort srcBp) {
            var type = GetResolvedPortType(destination, destinationPort);
            if (type != typeof(void))
                TryResolveTypeArg(source, srcBp.TypeParameterIndex, type);
        }
    }

    public Pipeline Build() {
        ResolveAllTypeArgs();

        var allInstances = new Dictionary<string, NodeInstance>();
        foreach (var node in _graph.Nodes)
            allInstances[node.ID] = node;

        foreach (var (id, genericRef) in _genericNodes) {
            if (!genericRef.IsResolved)
                throw new InvalidOperationException(
                    $"Generic node '{id}' has unresolved type parameters: {UnresolvedParamsDescription(genericRef)}");

            var concreteNode = genericRef.MaterializedNode
                ?? registry.GetOrCreateConcrete(genericRef.Blueprint.OpenType, genericRef.TypeArgs!);

            var mappings = new Dictionary<IPort, Guid>();
            foreach (var port in concreteNode.Ports) {
                if (genericRef.PortMappings.TryGetValue(port.Name, out var guid))
                    mappings[port] = guid;
                else
                    mappings[port] = Guid.NewGuid();
            }

            allInstances[id] = new NodeInstance(id, concreteNode, mappings);
        }

        var graph = new DirectedAcyclicGraph<NodeInstance>();
        foreach (var instance in allInstances.Values)
            graph.AddNode(instance);

        foreach (var edge in _edges) {
            graph.AddEdge(edge.SourceNode, edge.DestinationNode);
        }

        var allEdges = new List<EdgeInstance>(_edges);
        foreach (var pending in _pendingEdges) {
            var srcInstance = allInstances[pending.SourceId];
            var dstInstance = allInstances[pending.DestId];

            var srcPort = ResolvePort(srcInstance, pending.SourcePort);
            var dstPort = ResolvePort(dstInstance, pending.DestPort);

            ValidateTypeCompatibility(srcPort, dstPort);

            graph.AddEdge(srcInstance, dstInstance);
            dstInstance.Mappings[dstPort] = srcInstance.Mappings[srcPort];
            allEdges.Add(new EdgeInstance(srcInstance, srcPort, dstInstance, dstPort));
        }

        return new Pipeline(graph, [.. allEdges]);
    }

    private void CheckCycle(string sourceId, string destId) {
        if (sourceId == destId)
            throw new PipelineCycleException(sourceId, destId);

        if (!_adjacency.TryGetValue(sourceId, out var neighbors)) {
            neighbors = [];
            _adjacency[sourceId] = neighbors;
        }
        neighbors.Add(destId);

        if (HasPath(destId, sourceId, [])) {
            _adjacency[sourceId].Remove(destId);
            throw new PipelineCycleException(sourceId, destId);
        }
    }

    private bool HasPath(string from, string to, HashSet<string> visited) {
        if (from == to) return true;
        if (!visited.Add(from)) return false;
        if (!_adjacency.TryGetValue(from, out var neighbors)) return false;
        foreach (var next in neighbors) {
            if (HasPath(next, to, visited)) return true;
        }
        return false;
    }

    private void TryResolveTypeArg(GenericNodeRef node, int typeParamIndex, Type type) {
        if (typeParamIndex < 0 || typeParamIndex >= node.TypeArgs.Length) return;
        ref var current = ref node.TypeArgs[typeParamIndex];
        if (current is not null && current != type)
            throw new InvalidOperationException(
                $"Type conflict for '{node.ID}': type param '{node.Blueprint.TypeParameters[typeParamIndex].Name}' " +
                $"already resolved to '{current}' but connection requires '{type}'.");

        ValidateConstraints(node, typeParamIndex, type);

        current = type;

        if (node.IsResolved)
            node.Materialize();
    }

    private static void ValidateConstraints(GenericNodeRef node, int typeParamIndex, Type type) {
        var param = node.Blueprint.TypeParameters[typeParamIndex];
        var nodeId = node.ID;

        if ((param.Attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) {
            if (!type.IsValueType)
                throw new GenericConstraintException(nodeId, param, type, "must be a non-nullable value type (struct constraint)");
        }

        if ((param.Attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) {
            if (type.IsValueType)
                throw new GenericConstraintException(nodeId, param, type, "must be a reference type (class constraint)");
        }

        foreach (var rawConstraint in param.Constraints) {
            var constraint = SubstituteConstraint(rawConstraint, node.TypeArgs, typeParamIndex, type);
            if (!type.IsAssignableTo(constraint))
                throw new GenericConstraintException(nodeId, param, type, $"must implement or derive from '{constraint.Name}'");
        }

        if ((param.Attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) {
            if (!type.IsValueType && type.GetConstructor([]) == null)
                throw new GenericConstraintException(nodeId, param, type, "must have a public parameterless constructor (new() constraint)");
        }
    }

    private static Type SubstituteConstraint(Type constraint, Type?[] typeArgs, int resolvingIndex, Type resolvingType) {
        if (!constraint.IsGenericType) return constraint;

        var args = constraint.GetGenericArguments();
        var substituted = false;
        for (var i = 0; i < args.Length; i++) {
            if (!args[i].IsGenericParameter) continue;
            var pos = args[i].GenericParameterPosition;
            if (pos >= typeArgs.Length) continue;

            var resolved = typeArgs[pos] ?? (pos == resolvingIndex ? resolvingType : null);
            if (resolved is not null) {
                args[i] = resolved;
                substituted = true;
            }
        }

        return substituted ? constraint.GetGenericTypeDefinition().MakeGenericType(args) : constraint;
    }

    private static Type GetResolvedPortType(GenericNodeRef node, IPort port) {
        if (port is BlueprintPort bp) {
            if (bp.TypeParameterIndex < 0 || bp.TypeParameterIndex >= node.TypeArgs.Length)
                return typeof(void);
            return node.TypeArgs[bp.TypeParameterIndex] ?? typeof(void);
        }
        return port.PortType;
    }

    private void ResolveAllTypeArgs() {
        bool changed;
        do {
            changed = false;
            foreach (var pending in _pendingEdges) {
                var srcGeneric = _genericNodes.GetValueOrDefault(pending.SourceId);
                var dstGeneric = _genericNodes.GetValueOrDefault(pending.DestId);

                if (srcGeneric is not null && !srcGeneric.IsResolved) {
                    if (dstGeneric is not null && dstGeneric.IsResolved) {
                        var resolvedType = GetResolvedPortType(dstGeneric, pending.DestPort);
                        if (resolvedType != typeof(void) && pending.SourcePort is BlueprintPort srcBp) {
                            TryResolveTypeArg(srcGeneric, srcBp.TypeParameterIndex, resolvedType);
                            changed = true;
                        }
                    }
                }

                if (dstGeneric is not null && !dstGeneric.IsResolved) {
                    if (srcGeneric is not null && srcGeneric.IsResolved) {
                        var resolvedType = GetResolvedPortType(srcGeneric, pending.SourcePort);
                        if (resolvedType != typeof(void) && pending.DestPort is BlueprintPort dstBp) {
                            TryResolveTypeArg(dstGeneric, dstBp.TypeParameterIndex, resolvedType);
                            changed = true;
                        }
                    }
                }
            }
        } while (changed);
    }

    private static IPort ResolvePort(NodeInstance instance, IPort portRef) {
        if (portRef is BlueprintPort) {
            var found = instance.Node.Ports.FirstOrDefault(p => p.Name == portRef.Name);
            return found ?? throw new InvalidOperationException(
                $"Port '{portRef.Name}' not found on node {instance.Node.GetType().Name}");
        }
        return portRef;
    }

    private static string UnresolvedParamsDescription(GenericNodeRef genericRef) {
        var names = new List<string>();
        for (var i = 0; i < genericRef.TypeArgs.Length; i++) {
            if (genericRef.TypeArgs[i] is null)
                names.Add(genericRef.Blueprint.TypeParameters[i].Name);
        }
        return string.Join(", ", names);
    }

    private void ValidateTypeCompatibility(IPort sourcePort, IPort targetPort) {
        var sourceType = sourcePort.PortType;
        var targetType = targetPort.PortType;

        if (sourceType == targetType) return;
        if (sourceType.IsAssignableTo(targetType)) return;

        if (_castRegistry.TryGetCast(sourceType, targetType, out var rule)) {
            if (Config.StrictTypeCasting && rule!.CastType == TypeCast.Lossy)
                throw new TypeIncompatibilityException(sourcePort.Name, sourceType, targetPort.Name, targetType);
            return;
        }

        throw new TypeIncompatibilityException(sourcePort.Name, sourceType, targetPort.Name, targetType);
    }

    private readonly record struct PendingEdge(
        string SourceId, IPort SourcePort,
        string DestId, IPort DestPort
    );
}
