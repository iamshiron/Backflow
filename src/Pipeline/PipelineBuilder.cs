using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline;

public class PipelineBuilder(NodeRegistry registry) {
    public record class NodeInstance(string ID, AbstractNode Node, Dictionary<IPort, Guid> Mappings) {
        public NodeState State { get; set; } = NodeState.Pending;
        public virtual bool Equals(NodeInstance? other) {
            return other is not null && ID == other.ID;
        }
        public override int GetHashCode() {
            return ID.GetHashCode();
        }
    }

    public readonly record struct EdgeInstance(NodeInstance SourceNode, IPort SourcePort, NodeInstance DestinationNode, IPort DestinationPort);

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
        var fullName = node.GetType().FullName!;
        var id = NextId(fullName);

        var instance = new NodeInstance(
            id, node,
            node.Ports.ToDictionary(IPort (p) => p, _ => Guid.NewGuid())
        );

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

        var genericRef = new GenericNodeRef(id, blueprint, portMappings);
        _genericNodes[id] = genericRef;
        return genericRef;
    }

    public void AddConnection(NodeInstance source, IPort sourcePort, NodeInstance destination, IPort destinationPort) {
        if (!source.Node.Ports.Contains(sourcePort))
            throw new InvalidPortException(sourcePort, source.Node.GetType());

        if (!destination.Node.Ports.Contains(destinationPort))
            throw new InvalidPortException(destinationPort, destination.Node.GetType());

        CheckCycle(source.ID, destination.ID);

        _edges.Add(new EdgeInstance(source, sourcePort, destination, destinationPort));
        destination.Mappings[destinationPort] = source.Mappings[sourcePort];
    }

    public void AddConnection(NodeInstance source, IPort sourcePort, GenericNodeRef destination, BlueprintPort destinationPort) {
        CheckCycle(source.ID, destination.ID);
        _pendingEdges.Add(new PendingEdge(source.ID, sourcePort, destination.ID, destinationPort));

        ResolveTypeArgFromConcreteSource(destination, destinationPort, sourcePort);
    }

    public void AddConnection(GenericNodeRef source, BlueprintPort sourcePort, NodeInstance destination, IPort destinationPort) {
        CheckCycle(source.ID, destination.ID);
        _pendingEdges.Add(new PendingEdge(source.ID, sourcePort, destination.ID, destinationPort));

        if (source.IsResolved) return;
        var destType = destinationPort.PortType;
        if (destType == typeof(void)) return;
        TryResolveTypeArg(source, sourcePort.TypeParameterIndex, destType);
    }

    public void AddConnection(GenericNodeRef source, BlueprintPort sourcePort, GenericNodeRef destination, BlueprintPort destinationPort) {
        CheckCycle(source.ID, destination.ID);
        _pendingEdges.Add(new PendingEdge(source.ID, sourcePort, destination.ID, destinationPort));

        if (source.IsResolved && !destination.IsResolved) {
            TryResolveTypeArg(destination, destinationPort.TypeParameterIndex,
                GetResolvedPortType(source, sourcePort));
        } else if (destination.IsResolved && !source.IsResolved) {
            TryResolveTypeArg(source, sourcePort.TypeParameterIndex,
                GetResolvedPortType(destination, destinationPort));
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

            var concreteNode = registry.GetOrCreateConcrete(
                genericRef.Blueprint.OpenType,
                genericRef.TypeArgs!);

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

    private void ResolveTypeArgFromConcreteSource(GenericNodeRef dest, BlueprintPort destPort, IPort sourcePort) {
        if (dest.IsResolved) return;
        var sourceType = sourcePort.PortType;
        if (sourceType == typeof(void)) return;
        TryResolveTypeArg(dest, destPort.TypeParameterIndex, sourceType);
    }

    private void TryResolveTypeArg(GenericNodeRef node, int typeParamIndex, Type type) {
        if (typeParamIndex < 0 || typeParamIndex >= node.TypeArgs.Length) return;
        ref var current = ref node.TypeArgs[typeParamIndex];
        if (current is not null && current != type)
            throw new InvalidOperationException(
                $"Type conflict for '{node.ID}': type param '{node.Blueprint.TypeParameters[typeParamIndex].Name}' " +
                $"already resolved to '{current}' but connection requires '{type}'.");
        current = type;
    }

    private Type GetResolvedPortType(GenericNodeRef node, BlueprintPort port) {
        if (port.TypeParameterIndex < 0 || port.TypeParameterIndex >= node.TypeArgs.Length)
            return typeof(void);
        return node.TypeArgs[port.TypeParameterIndex] ?? typeof(void);
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
                        var resolvedType = GetResolvedPortType(dstGeneric, (BlueprintPort) pending.DestPort);
                        if (resolvedType != typeof(void)) {
                            TryResolveTypeArg(srcGeneric, ((BlueprintPort) pending.SourcePort).TypeParameterIndex, resolvedType);
                            changed = true;
                        }
                    }
                }

                if (dstGeneric is not null && !dstGeneric.IsResolved) {
                    if (srcGeneric is not null && srcGeneric.IsResolved) {
                        var resolvedType = GetResolvedPortType(srcGeneric, (BlueprintPort) pending.SourcePort);
                        if (resolvedType != typeof(void)) {
                            TryResolveTypeArg(dstGeneric, ((BlueprintPort) pending.DestPort).TypeParameterIndex, resolvedType);
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

    private readonly record struct PendingEdge(
        string SourceId, IPort SourcePort,
        string DestId, IPort DestPort
    );
}
