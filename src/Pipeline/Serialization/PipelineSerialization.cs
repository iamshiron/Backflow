using System.Text.Json;
using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Pipeline.Serialization;

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
                n.Mappings.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value),
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
    public static PipelineInputsDto ToInputsDto(this Pipeline pipeline, PipelineContext context) {
        var inputs = new Dictionary<string, Dictionary<string, InputDto>>();

        foreach (var node in pipeline.Topology.Nodes) {
            var nodeInputs = new Dictionary<string, InputDto>();
            foreach (var (port, mappingGuid) in node.Mappings) {
                if (!context.Store.HasAny(mappingGuid)) continue;

                nodeInputs[port.Name] = new InputDto(
                    context.ReadAny(mappingGuid),
                    context.Store.TypeOf(mappingGuid)?.FullName ??
                        context.Store.TypeOf(mappingGuid)?.Name ??
                        throw new InvalidOperationException("Unable to determine type of input")
                );
            }

            if (nodeInputs.Count > 0) {
                inputs[node.ID] = nodeInputs;
            }
        }

        return new PipelineInputsDto(inputs);
    }

    /// <summary>Reconstruct a <see cref="Pipeline"/> from a definition DTO using the given registry to resolve node types.</summary>
    public static Pipeline FromDefinitionDto(this PipelineDefinitionDto dto, NodeRegistry registry) {
        var arrayCounts = BuildArrayCounts(dto.Edges);

        var nodeInstances = new Dictionary<string, PipelineBuilder.NodeInstance>();

        foreach (var nodeDto in dto.Nodes) {
            AbstractNode node;

            if (nodeDto.GenericTypeArgs is { Length: > 0 }) {
                var blueprint = registry.GetBlueprint(nodeDto.NodeTypeName)
                    ?? throw new InvalidOperationException($"Generic node blueprint not registered: {nodeDto.NodeTypeName}");

                var typeArgs = new Type[nodeDto.GenericTypeArgs.Length];
                for (var i = 0; i < nodeDto.GenericTypeArgs.Length; i++) {
                    typeArgs[i] = Type.GetType(nodeDto.GenericTypeArgs[i])
                        ?? throw new InvalidOperationException($"Cannot resolve type: {nodeDto.GenericTypeArgs[i]}");
                }

                node = registry.GetOrCreateConcrete(blueprint.OpenType, typeArgs);
            } else {
                node = registry.GetByFullName(nodeDto.NodeTypeName)
                    ?? throw new InvalidOperationException($"Node type not registered in registry: {nodeDto.NodeTypeName}");
            }

            var nodeArrayCounts = arrayCounts.GetValueOrDefault(nodeDto.Id, new Dictionary<string, int>());

            var builder = new PipelineBuilder(registry);
            var instance = builder.AddNode(node, nodeArrayCounts);

            foreach (var port in node.Ports) {
                if (nodeDto.PortMappings.TryGetValue(port.Name, out var mappingGuid)) {
                    instance.Mappings[port] = mappingGuid;
                }
            }

            nodeInstances[nodeDto.Id] = instance;
        }

        var graph = new DirectedAcyclicGraph<PipelineBuilder.NodeInstance>();
        foreach (var instance in nodeInstances.Values)
            graph.AddNode(instance);

        var edges = new PipelineBuilder.EdgeInstance[dto.Edges.Length];
        for (var i = 0; i < dto.Edges.Length; i++) {
            var edgeDto = dto.Edges[i];

            var source = nodeInstances[edgeDto.SourceNodeId];
            var dest = nodeInstances[edgeDto.DestinationNodeId];

            var sourcePort = source.Node.Ports.FirstOrDefault(p => p.Name == edgeDto.SourcePortName)
                ?? throw new InvalidOperationException($"Source port '{edgeDto.SourcePortName}' not found on node {edgeDto.SourceNodeId}");

            var destPort = dest.Node.Ports.FirstOrDefault(p => p.Name == edgeDto.DestinationPortName)
                ?? throw new InvalidOperationException($"Destination port '{edgeDto.DestinationPortName}' not found on node {edgeDto.DestinationNodeId}");

            if (!edgeDto.DestIndex.HasValue) {
                dest.Mappings[destPort] = source.Mappings[sourcePort];
            }

            graph.AddEdge(source, dest);
            edges[i] = new PipelineBuilder.EdgeInstance(source, sourcePort, dest, destPort, edgeDto.DestIndex);
        }

        return new Pipeline(graph, edges);
    }

    /// <summary>Restore input values from a DTO into a new <see cref="PipelineContext"/>.</summary>
    public static PipelineContext FromInputs(this PipelineInputsDto dto, Pipeline pipeline, IServiceProvider? serviceProvider = null) {
        var context = new PipelineContext(serviceProvider);
        var nodeLookup = pipeline.Topology.Nodes.ToDictionary(n => n.ID);

        foreach (var (nodeId, portInputs) in dto.Inputs) {
            if (!nodeLookup.TryGetValue(nodeId, out var node))
                throw new InvalidOperationException($"Node '{nodeId}' not found in pipeline.");

            foreach (var (portKey, inputDto) in portInputs) {
                var type = Type.GetType(inputDto.Type)
                    ?? throw new InvalidOperationException($"Cannot resolve type: {inputDto.Type}");

                var value = inputDto.Value is JsonElement je
                    ? je.Deserialize(type)
                    : inputDto.Value;

                var port = node.Node.Ports.FirstOrDefault(p => p.Name == portKey)
                    ?? throw new InvalidOperationException($"Port '{portKey}' not found on node '{nodeId}'.");
                var mappingGuid = node.Mappings[port];
                context.Store.Set(mappingGuid, value, type);
            }
        }
        return context;
    }

    /// <summary>Serialize the pipeline topology to a JSON string.</summary>
    public static string SerializeDefinition(this Pipeline pipeline, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToDefinitionDto(), options);
    }

    /// <summary>Serialize the current input values to a JSON string.</summary>
    public static string SerializeInputs(this Pipeline pipeline, PipelineContext context, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToInputsDto(context), options);
    }

    /// <summary>Deserialize a pipeline topology from JSON, resolving node types via <paramref name="registry"/>.</summary>
    public static Pipeline DeserializeDefinition(string json, NodeRegistry registry, JsonSerializerOptions? options = null) {
        var dto = JsonSerializer.Deserialize<PipelineDefinitionDto>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize pipeline definition JSON.");

        return dto.FromDefinitionDto(registry);
    }

    /// <summary>Deserialize input values from JSON into a <see cref="PipelineContext"/> bound to <paramref name="pipeline"/>.</summary>
    public static PipelineContext DeserializeInputs(string json, Pipeline pipeline, IServiceProvider? serviceProvider = null, JsonSerializerOptions? options = null) {
        var dto = JsonSerializer.Deserialize<PipelineInputsDto>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize pipeline inputs JSON.");

        return dto.FromInputs(pipeline, serviceProvider);
    }

    private static Dictionary<string, Dictionary<string, int>> BuildArrayCounts(EdgeDto[] edges) {
        var result = new Dictionary<string, Dictionary<string, int>>();

        foreach (var edge in edges) {
            if (!edge.DestIndex.HasValue) continue;

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
