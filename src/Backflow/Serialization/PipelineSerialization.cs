using System.Text.Json;
using Shiron.Lib.Collections;
using Shiron.Backflow.Casting;
using Shiron.Backflow.Context;
using Shiron.Backflow.Exceptions;
using Shiron.Backflow.Generic;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;

namespace Shiron.Backflow.Serialization;

/// <summary>
/// Static API for serializing and deserializing <see cref="Pipeline"/> topologies and input snapshots to/from JSON.
/// </summary>
public static class PipelineSerialization {
    /// <summary>Convert a pipeline topology to a serializable DTO.</summary>
    public static PipelineDefinitionDto ToDefinitionDto(this Pipeline pipeline) {
        var nodes = pipeline.Topology.Nodes.Select(n => {
            var nodeType = n.Node.GetType();
            string[]? genericArgs = null;
            string typeName;

            if (nodeType.IsGenericType) {
                typeName = nodeType.GetGenericTypeDefinition().FullName!;
                genericArgs = nodeType.GetGenericArguments().Select(a => a.FullName ?? a.Name).ToArray();
            } else {
                typeName = nodeType.FullName!;
            }

            return new NodeInstanceDto(
                n.ID,
                typeName,
                genericArgs
            );
        }).ToArray();

        var edges = pipeline.Edges.Select(e => new EdgeDto(
            e.SourceNode.ID,
            e.SourcePort.Name,
            e.DestinationNode.ID,
            e.DestinationPort.Name,
            e.DestIndex
        )).ToArray();

        return new PipelineDefinitionDto(nodes, edges);
    }

    /// <summary>Capture current input values from the context as a serializable DTO.</summary>
    public static PipelineInputsDto ToInputsDto(this Pipeline pipeline, IPipelineContext context) {
        var inputs = new Dictionary<string, Dictionary<string, InputDto>>();

        foreach (var node in pipeline.Topology.Nodes) {
            var nodeInputs = new Dictionary<string, InputDto>();
            foreach (var (port, channel) in node.Mappings) {
                if (!context.HasAny(channel)) continue;

                var type = context.TypeOf(channel)
                    ?? throw new InvalidOperationException("Unable to determine type of input");

                nodeInputs[port.Name] = new InputDto(
                    context.ReadAny(channel),
                    type.FullName ?? type.Name,
                    context.GetSuppliedMask(channel)
                );
            }

            if (nodeInputs.Count > 0) {
                inputs[node.ID] = nodeInputs;
            }
        }

        return new PipelineInputsDto(inputs);
    }

    /// <summary>
    /// Reconstruct a <see cref="Pipeline"/> from a definition DTO using the given registry to resolve node types.
    /// All recoverable validation errors (missing nodes, missing ports, cycles, etc.) are accumulated and
    /// thrown as a single <see cref="PipelineDeserializationException"/> so every problem is visible at once.
    /// Channels are not serialized; a fresh channel is assigned to every port, then scalar edges unify
    /// the destination input channel onto its source output channel, restoring shared-memory connectivity.
    /// </summary>
    public static Pipeline FromDefinitionDto(this PipelineDefinitionDto dto, NodeRegistry registry) {
        ArgumentNullException.ThrowIfNull(registry);

        var errors = new DeserializationErrorCollector();
        var nodeInstances = new Dictionary<string, PipelineBuilder.NodeInstance>();
        var resolvedEdges = new List<(PipelineBuilder.NodeInstance Source, IPort SourcePort, PipelineBuilder.NodeInstance Dest, IPort DestPort, int? DestIndex)>();
        var adjacency = new Dictionary<string, HashSet<string>>();
        var nextChannel = 0;

        var validNodeIds = ValidateDefinitionStructure();
        ResolveDefinitionNodes(validNodeIds);
        ResolveDefinitionEdges(validNodeIds);
        errors.ThrowIfErrors();
        return BuildPipeline();

        // --- Phase 1: Structural validation ---

        HashSet<string> ValidateDefinitionStructure() {
            var validIds = new HashSet<string>();

            if (dto.Nodes is null) {
                errors.Add(DeserializationPhase.Structure, "The 'Nodes' array is null.");
            } else {
                var seenIds = new HashSet<string>();
                for (var i = 0; i < dto.Nodes.Length; i++) {
                    var nodeDto = dto.Nodes[i];
                    if (nodeDto is null) {
                        errors.Add(DeserializationPhase.Structure, $"Node at index {i} is null.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(nodeDto.Id)) {
                        errors.Add(DeserializationPhase.Structure, $"Node at index {i} has a null or empty ID.");
                        continue;
                    }
                    if (!seenIds.Add(nodeDto.Id)) {
                        errors.Add(DeserializationPhase.Structure, $"Duplicate node ID '{nodeDto.Id}'.", nodeId: nodeDto.Id);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(nodeDto.NodeTypeName)) {
                        errors.Add(DeserializationPhase.Structure, $"Node '{nodeDto.Id}' has a null or empty type name.", nodeId: nodeDto.Id);
                        continue;
                    }
                    validIds.Add(nodeDto.Id);
                }
            }

            if (dto.Edges is not null) {
                for (var i = 0; i < dto.Edges.Length; i++) {
                    var edgeDto = dto.Edges[i];
                    if (edgeDto is null) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} is null.", edgeIndex: i);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(edgeDto.SourceNodeId)) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} has a null or empty source node ID.", edgeIndex: i);
                    } else if (!validIds.Contains(edgeDto.SourceNodeId)) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} references unknown source node '{edgeDto.SourceNodeId}'.", edgeIndex: i);
                    }
                    if (string.IsNullOrWhiteSpace(edgeDto.DestinationNodeId)) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} has a null or empty destination node ID.", edgeIndex: i);
                    } else if (!validIds.Contains(edgeDto.DestinationNodeId)) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} references unknown destination node '{edgeDto.DestinationNodeId}'.", edgeIndex: i);
                    }
                    if (string.IsNullOrWhiteSpace(edgeDto.SourcePortName)) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} has a null or empty source port name.", edgeIndex: i);
                    }
                    if (string.IsNullOrWhiteSpace(edgeDto.DestinationPortName)) {
                        errors.Add(DeserializationPhase.Structure, $"Edge at index {i} has a null or empty destination port name.", edgeIndex: i);
                    }
                }
            }

            return validIds;
        }

        // --- Phase 2: Node resolution ---

        void ResolveDefinitionNodes(HashSet<string> validNodeIds) {
            if (dto.Nodes is null) return;
            var arrayCounts = BuildArrayCounts(dto.Edges);

            foreach (var nodeDto in dto.Nodes) {
                if (nodeDto is null || !validNodeIds.Contains(nodeDto.Id))
                    continue;

                AbstractNode? node;

                if (nodeDto.GenericTypeArgs is { Length: > 0 }) {
                    var blueprint = registry.GetBlueprint(nodeDto.NodeTypeName);
                    if (blueprint is null) {
                        var typeArgsStr = string.Join(", ", nodeDto.GenericTypeArgs);
                        errors.Add(DeserializationPhase.NodeResolution,
                            $"Generic node blueprint '{nodeDto.NodeTypeName}' is not registered. Type arguments: [{typeArgsStr}].",
                            nodeId: nodeDto.Id);
                        continue;
                    }

                    var typeArgs = new Type[nodeDto.GenericTypeArgs.Length];
                    var allResolved = true;
                    for (var i = 0; i < nodeDto.GenericTypeArgs.Length; i++) {
                        typeArgs[i] = ResolveType(nodeDto.GenericTypeArgs[i])!;
                        if (typeArgs[i] is null) {
                            errors.Add(DeserializationPhase.TypeResolution,
                                $"Cannot resolve generic type argument '{nodeDto.GenericTypeArgs[i]}' for node type '{nodeDto.NodeTypeName}'.",
                                nodeId: nodeDto.Id);
                            allResolved = false;
                        }
                    }
                    if (!allResolved) continue;

                    try {
                        node = registry.GetOrCreateConcrete(blueprint.OpenType, typeArgs);
                    } catch (Exception ex) {
                        errors.Add(DeserializationPhase.NodeResolution,
                            $"Failed to instantiate generic node '{nodeDto.NodeTypeName}': {ex.Message}",
                            nodeId: nodeDto.Id);
                        continue;
                    }
                } else {
                    node = registry.GetByFullName(nodeDto.NodeTypeName);
                    if (node is null) {
                        errors.Add(DeserializationPhase.NodeResolution,
                            $"Node type '{nodeDto.NodeTypeName}' is not registered.",
                            nodeId: nodeDto.Id);
                        continue;
                    }
                }

                var nodeArrayCounts = arrayCounts.GetValueOrDefault(nodeDto.Id, new Dictionary<string, int>());

                var mappings = new Dictionary<IPort, int>();
                Dictionary<IPort, int>? instanceArrayCounts = null;
                foreach (var port in node.Ports) {
                    mappings[port] = nextChannel++;

                    if (port is IArrayInputPortMarker arrayPort && nodeArrayCounts.TryGetValue(port.Name, out var count)) {
                        try {
                            arrayPort.ValidateCount(count);
                        } catch (Exception ex) {
                            errors.Add(DeserializationPhase.PortResolution, ex.Message, nodeId: nodeDto.Id, portName: port.Name);
                        }
                        (instanceArrayCounts ??= new Dictionary<IPort, int>())[port] = count;
                    }
                }

                nodeInstances[nodeDto.Id] = new PipelineBuilder.NodeInstance(nodeDto.Id, node, mappings, instanceArrayCounts);
            }
        }

        // --- Phase 3: Edge resolution (ports, array indices, cycles) ---

        void ResolveDefinitionEdges(HashSet<string> validNodeIds) {
            if (dto.Edges is null) return;

            for (var i = 0; i < dto.Edges.Length; i++) {
                var edgeDto = dto.Edges[i];
                if (edgeDto is null) continue;

                if (!validNodeIds.Contains(edgeDto.SourceNodeId) || !validNodeIds.Contains(edgeDto.DestinationNodeId))
                    continue;
                if (string.IsNullOrWhiteSpace(edgeDto.SourcePortName) || string.IsNullOrWhiteSpace(edgeDto.DestinationPortName))
                    continue;
                if (!nodeInstances.TryGetValue(edgeDto.SourceNodeId, out var source)) continue;
                if (!nodeInstances.TryGetValue(edgeDto.DestinationNodeId, out var dest)) continue;

                var sourcePort = source.Node.Ports.FirstOrDefault(p => p.Name == edgeDto.SourcePortName);
                if (sourcePort is null) {
                    var available = string.Join(", ", source.Node.Ports.Select(p => p.Name));
                    errors.Add(DeserializationPhase.PortResolution,
                        $"Source port '{edgeDto.SourcePortName}' not found on node type '{source.Node.GetType().Name}'. Available ports: [{available}].",
                        nodeId: edgeDto.SourceNodeId, portName: edgeDto.SourcePortName, edgeIndex: i);
                    continue;
                }

                var destPort = dest.Node.Ports.FirstOrDefault(p => p.Name == edgeDto.DestinationPortName);
                if (destPort is null) {
                    var available = string.Join(", ", dest.Node.Ports.Select(p => p.Name));
                    errors.Add(DeserializationPhase.PortResolution,
                        $"Destination port '{edgeDto.DestinationPortName}' not found on node type '{dest.Node.GetType().Name}'. Available ports: [{available}].",
                        nodeId: edgeDto.DestinationNodeId, portName: edgeDto.DestinationPortName, edgeIndex: i);
                    continue;
                }

                if (edgeDto.DestIndex.HasValue) {
                    if (destPort is not IArrayInputPortMarker) {
                        errors.Add(DeserializationPhase.PortResolution,
                            $"Destination port '{edgeDto.DestinationPortName}' is not an array input port, but edge specifies index {edgeDto.DestIndex.Value}.",
                            nodeId: edgeDto.DestinationNodeId, portName: edgeDto.DestinationPortName, edgeIndex: i);
                        continue;
                    }
                    if (dest.ArrayCounts?.TryGetValue(destPort, out var count) == true) {
                        if (edgeDto.DestIndex.Value < 0 || edgeDto.DestIndex.Value >= count) {
                            errors.Add(DeserializationPhase.PortResolution,
                                $"Array index {edgeDto.DestIndex.Value} is out of range for port '{edgeDto.DestinationPortName}' (count: {count}).",
                                nodeId: edgeDto.DestinationNodeId, portName: edgeDto.DestinationPortName, edgeIndex: i);
                            continue;
                        }
                    }
                }

                if (edgeDto.SourceNodeId == edgeDto.DestinationNodeId) {
                    errors.Add(DeserializationPhase.Graph,
                        $"Self-loop detected: node '{edgeDto.SourceNodeId}' connects to itself.",
                        nodeId: edgeDto.SourceNodeId, edgeIndex: i);
                    continue;
                }

                if (HasGraphPath(edgeDto.DestinationNodeId, edgeDto.SourceNodeId)) {
                    errors.Add(DeserializationPhase.Graph,
                        $"Edge from '{edgeDto.SourceNodeId}' to '{edgeDto.DestinationNodeId}' would create a cycle.",
                        edgeIndex: i);
                    continue;
                }

                if (!adjacency.TryGetValue(edgeDto.SourceNodeId, out var neighbors)) {
                    neighbors = [];
                    adjacency[edgeDto.SourceNodeId] = neighbors;
                }
                neighbors.Add(edgeDto.DestinationNodeId);

                resolvedEdges.Add((source, sourcePort, dest, destPort, edgeDto.DestIndex));
            }
        }

        // --- Phase 4: Build pipeline (all validation passed) ---

        Pipeline BuildPipeline() {
            var graph = new DirectedAcyclicGraph<PipelineBuilder.NodeInstance>();
            foreach (var instance in nodeInstances.Values)
                graph.AddNode(instance);

            var edges = new PipelineBuilder.EdgeInstance[resolvedEdges.Count];
            for (var i = 0; i < resolvedEdges.Count; i++) {
                var (source, sourcePort, dest, destPort, destIndex) = resolvedEdges[i];

                if (!destIndex.HasValue) {
                    dest.Mappings[destPort] = source.Mappings[sourcePort];
                }

                graph.AddEdge(source, dest);
                edges[i] = new PipelineBuilder.EdgeInstance(source, sourcePort, dest, destPort, destIndex);
            }

            return new Pipeline(graph, edges, CastRegistry.CreateDefault());
        }

        bool HasGraphPath(string from, string to) {
            if (from == to) return true;
            var visited = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(from);
            while (stack.Count > 0) {
                var current = stack.Pop();
                if (!visited.Add(current)) continue;
                if (adjacency.TryGetValue(current, out var neighbors)) {
                    foreach (var n in neighbors) {
                        if (n == to) return true;
                        stack.Push(n);
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Restore input values from a DTO into a new <see cref="ArrayPipelineContext"/>.
    /// All recoverable validation errors are accumulated and thrown as a single
    /// <see cref="PipelineDeserializationException"/>.
    /// </summary>
    public static ArrayPipelineContext FromInputs(this PipelineInputsDto dto, Pipeline pipeline) {
        var errors = new DeserializationErrorCollector();
        var nodeLookup = pipeline.Topology.Nodes.ToDictionary(n => n.ID);
        var validatedEntries = new List<(string NodeId, string PortKey, InputDto InputDto, Type Type, object? Value)>();

        ValidateInputs();
        errors.ThrowIfErrors();
        return ApplyInputs();

        void ValidateInputs() {
            if (dto.Inputs is null) {
                errors.Add(DeserializationPhase.Structure, "The 'Inputs' dictionary is null.");
                return;
            }

            foreach (var (nodeId, portInputs) in dto.Inputs) {
                if (!nodeLookup.TryGetValue(nodeId, out var node)) {
                    errors.Add(DeserializationPhase.NodeResolution, $"Node '{nodeId}' not found in pipeline.", nodeId: nodeId);
                    continue;
                }
                if (portInputs is null) {
                    errors.Add(DeserializationPhase.Structure, $"Port inputs for node '{nodeId}' is null.", nodeId: nodeId);
                    continue;
                }

                foreach (var (portKey, inputDto) in portInputs) {
                    if (inputDto is null) {
                        errors.Add(DeserializationPhase.Structure, $"Input for port '{portKey}' on node '{nodeId}' is null.", nodeId: nodeId, portName: portKey);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(inputDto.Type)) {
                        errors.Add(DeserializationPhase.TypeResolution, $"Port '{portKey}' on node '{nodeId}' has a null or empty type name.", nodeId: nodeId, portName: portKey);
                        continue;
                    }

                    var type = ResolveType(inputDto.Type);
                    if (type is null) {
                        errors.Add(DeserializationPhase.TypeResolution, $"Cannot resolve type '{inputDto.Type}' for port '{portKey}' on node '{nodeId}'.", nodeId: nodeId, portName: portKey);
                        continue;
                    }

                    var port = node.Node.Ports.FirstOrDefault(p => p.Name == portKey);
                    if (port is null) {
                        var available = string.Join(", ", node.Node.Ports.Select(p => p.Name));
                        errors.Add(DeserializationPhase.PortResolution, $"Port '{portKey}' not found on node type '{node.Node.GetType().Name}'. Available ports: [{available}].", nodeId: nodeId, portName: portKey);
                        continue;
                    }

                    object? value;
                    try {
                        value = inputDto.Value is JsonElement je ? je.Deserialize(type) : inputDto.Value;
                    } catch (Exception ex) {
                        errors.Add(DeserializationPhase.ValueResolution, $"Failed to deserialize value for port '{portKey}' on node '{nodeId}' as type '{type.Name}': {ex.Message}", nodeId: nodeId, portName: portKey);
                        continue;
                    }

                    validatedEntries.Add((nodeId, portKey, inputDto, type, value));
                }
            }
        }

        ArrayPipelineContext ApplyInputs() {
            var context = ArrayPipelineContext.ForPipeline(pipeline);

            foreach (var (nodeId, portKey, inputDto, type, value) in validatedEntries) {
                var node = nodeLookup[nodeId];
                var port = node.Node.Ports.First(p => p.Name == portKey);
                var channel = node.Mappings[port];
                context.Write(channel, value, type);

                if (inputDto.SuppliedMask is not null) {
                    context.SetSuppliedMask(channel, inputDto.SuppliedMask);
                }
            }

            return context;
        }
    }

    /// <summary>Serialize the pipeline topology to a JSON string.</summary>
    public static string SerializeDefinition(this Pipeline pipeline, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToDefinitionDto(), options);
    }

    /// <summary>Serialize the current input values to a JSON string.</summary>
    public static string SerializeInputs(this Pipeline pipeline, IPipelineContext context, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToInputsDto(context), options);
    }

    /// <summary>
    /// Deserialize a pipeline topology from JSON, resolving node types via <paramref name="registry"/>.
    /// JSON parse failures and all structural/semantic validation errors are wrapped in a
    /// <see cref="PipelineDeserializationException"/>.
    /// </summary>
    public static Pipeline DeserializeDefinition(string json, NodeRegistry registry, JsonSerializerOptions? options = null) {
        ArgumentNullException.ThrowIfNull(json);

        PipelineDefinitionDto dto;
        try {
            dto = JsonSerializer.Deserialize<PipelineDefinitionDto>(json, options)!;
        } catch (JsonException ex) {
            throw new PipelineDeserializationException(
                new DeserializationError(DeserializationPhase.JsonParsing, $"Invalid JSON: {ex.Message}"), ex);
        }

        if (dto is null) {
            throw new PipelineDeserializationException(
                new DeserializationError(DeserializationPhase.JsonParsing, "Pipeline definition JSON deserialized to null."));
        }

        return dto.FromDefinitionDto(registry);
    }

    /// <summary>
    /// Deserialize input values from JSON into an <see cref="ArrayPipelineContext"/> bound to <paramref name="pipeline"/>.
    /// JSON parse failures and all structural/semantic validation errors are wrapped in a
    /// <see cref="PipelineDeserializationException"/>.
    /// </summary>
    public static ArrayPipelineContext DeserializeInputs(string json, Pipeline pipeline, JsonSerializerOptions? options = null) {
        ArgumentNullException.ThrowIfNull(json);

        PipelineInputsDto dto;
        try {
            dto = JsonSerializer.Deserialize<PipelineInputsDto>(json, options)!;
        } catch (JsonException ex) {
            throw new PipelineDeserializationException(
                new DeserializationError(DeserializationPhase.JsonParsing, $"Invalid JSON: {ex.Message}"), ex);
        }

        if (dto is null) {
            throw new PipelineDeserializationException(
                new DeserializationError(DeserializationPhase.JsonParsing, "Pipeline inputs JSON deserialized to null."));
        }

        return dto.FromInputs(pipeline);
    }

    private static Type? ResolveType(string typeName) {
        var type = Type.GetType(typeName);
        if (type is not null) return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            type = assembly.GetType(typeName);
            if (type is not null) return type;
        }

        return null;
    }

    private static Dictionary<string, Dictionary<string, int>> BuildArrayCounts(EdgeDto[]? edges) {
        var result = new Dictionary<string, Dictionary<string, int>>();
        if (edges is null) return result;

        foreach (var edge in edges) {
            if (edge is null || !edge.DestIndex.HasValue) continue;

            if (!result.TryGetValue(edge.DestinationNodeId, out var nodePorts)) {
                nodePorts = [];
                result[edge.DestinationNodeId] = nodePorts;
            }

            ref var current = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(nodePorts, edge.DestinationPortName, out _);
            var needed = edge.DestIndex.Value + 1;
            if (needed > current) current = needed;
        }

        return result;
    }
}
