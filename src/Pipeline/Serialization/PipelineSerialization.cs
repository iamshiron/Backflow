using System.Text.Json;
using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Serialization;

/// <summary>JSON serialization/deserialization extensions for <see cref="Pipeline"/>.</summary>
public static class PipelineSerialization {
    /// <summary>Convert a <see cref="Pipeline"/> to its definition DTO (topology + edges).</summary>
    public static PipelineDefinitionDto ToDefinitionDto(this Pipeline pipeline) {
        var nodes = pipeline.Topology.Nodes.Select(n => new NodeInstanceDto(
            n.ID,
            n.Node.GetType().FullName!,
            n.Mappings.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value)
        )).ToArray();

        var edges = pipeline.Edges.Select(e => new EdgeDto(
            e.SourceNode.ID,
            e.SourcePort.Name,
            e.DestinationNode.ID,
            e.DestinationPort.Name
        )).ToArray();

        return new PipelineDefinitionDto(nodes, edges);
    }

    /// <summary>Convert pipeline inputs to a DTO keyed by node ID and port name.</summary>
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

    /// <summary>Reconstruct a <see cref="Pipeline"/> from its definition DTO. Requires a populated <paramref name="registry"/>.</summary>
    public static Pipeline FromDefinitionDto(this PipelineDefinitionDto dto, NodeRegistry registry) {
        var nodeInstances = new Dictionary<string, PipelineBuilder.NodeInstance>();

        foreach (var nodeDto in dto.Nodes) {
            var node = registry.GetByFullName(nodeDto.NodeTypeName)
                ?? throw new InvalidOperationException($"Node type not registered in registry: {nodeDto.NodeTypeName}");

            var mappings = new Dictionary<IPort, Guid>();
            foreach (var (portName, mappingId) in nodeDto.PortMappings) {
                var port = node.Ports.FirstOrDefault(p => p.Name == portName)
                    ?? throw new InvalidOperationException($"Port '{portName}' not found on node type {nodeDto.NodeTypeName}");

                mappings[port] = mappingId;
            }

            nodeInstances[nodeDto.Id] = new PipelineBuilder.NodeInstance(nodeDto.Id, node, mappings);
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

            graph.AddEdge(source, dest);
            edges[i] = new PipelineBuilder.EdgeInstance(source, sourcePort, dest, destPort);
        }

        return new Pipeline(graph, edges);
    }

    /// <summary>Reconstruct a <see cref="PipelineContext"/> from an inputs DTO.</summary>
    public static PipelineContext FromInputs(this PipelineInputsDto dto, Pipeline pipeline) {
        var context = new PipelineContext();
        var nodeLookup = pipeline.Topology.Nodes.ToDictionary(n => n.ID);

        foreach (var (nodeId, portInputs) in dto.Inputs) {
            if (!nodeLookup.TryGetValue(nodeId, out var node))
                throw new InvalidOperationException($"Node '{nodeId}' not found in pipeline.");

            foreach (var (portName, inputDto) in portInputs) {
                var port = node.Node.Ports.FirstOrDefault(p => p.Name == portName)
                    ?? throw new InvalidOperationException($"Port '{portName}' not found on node '{nodeId}'.");

                var mappingGuid = node.Mappings[port];
                var type = Type.GetType(inputDto.Type)
                    ?? throw new InvalidOperationException($"Cannot resolve type: {inputDto.Type}");

                var value = inputDto.Value is JsonElement je
                    ? je.Deserialize(type)
                    : inputDto.Value;

                context.Store.Set(mappingGuid, value, type);
            }
        }
        return context;
    }

    /// <summary>Serialize a <see cref="Pipeline"/> definition (topology + edges) to JSON.</summary>
    public static string SerializeDefinition(this Pipeline pipeline, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToDefinitionDto(), options);
    }

    /// <summary>Serialize pipeline inputs to JSON, keyed by node ID and port name.</summary>
    public static string SerializeInputs(this Pipeline pipeline, PipelineContext context, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToInputsDto(context), options);
    }

    /// <summary>Deserialize a <see cref="Pipeline"/> definition from JSON. Requires a populated <paramref name="registry"/>.</summary>
    public static Pipeline DeserializeDefinition(string json, NodeRegistry registry, JsonSerializerOptions? options = null) {
        var dto = JsonSerializer.Deserialize<PipelineDefinitionDto>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize pipeline definition JSON.");

        return dto.FromDefinitionDto(registry);
    }

    /// <summary>Deserialize pipeline inputs from JSON. Requires the <paramref name="pipeline"/> to resolve node ports.</summary>
    public static PipelineContext DeserializeInputs(string json, Pipeline pipeline, JsonSerializerOptions? options = null) {
        var dto = JsonSerializer.Deserialize<PipelineInputsDto>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize pipeline inputs JSON.");

        return dto.FromInputs(pipeline);
    }
}
