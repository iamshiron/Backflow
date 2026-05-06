using System.Text.Json;
using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Serialization;

/// <summary>JSON serialization/deserialization extensions for <see cref="Pipeline"/>.</summary>
public static class PipelineSerialization {
    /// <summary>Convert a <see cref="Pipeline"/> to its DTO representation.</summary>
    /// <param name="pipeline">Pipeline to convert.</param>
    public static PipelineDto ToDto(this Pipeline pipeline, PipelineContext? context = null) {
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

        var inputs = context == null
            ? []
            : context.Store.Keys.ToDictionary(k => k,
                k => new InputDto(context.ReadAny(k),
                    context.Store.TypeOf(k)?.FullName ??
                    context.Store.TypeOf(k)?.Name ?? throw new InvalidOperationException("Unable to determine type of input")
                )
            );

        return new PipelineDto(nodes, edges, inputs);
    }

    /// <summary>Reconstruct a <see cref="Pipeline"/> from its DTO. Requires a populated <paramref name="registry"/>.</summary>
    /// <param name="dto">DTO to reconstruct from.</param>
    /// <param name="registry">Registry containing all node types referenced in the DTO.</param>
    public static Pipeline FromPipelineDto(this PipelineDto dto, NodeRegistry registry) {
        var nodeInstances = new Dictionary<Guid, PipelineBuilder.NodeInstance>();

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

    public static PipelineContext FromInputs(this PipelineDto dto) {
        var context = new PipelineContext();
        foreach (var (key, inputDto) in dto.Inputs) {
            var type = Type.GetType(inputDto.Type)
                ?? throw new InvalidOperationException($"Cannot resolve type: {inputDto.Type}");

            var value = inputDto.Value is JsonElement je
                ? je.Deserialize(type)
                : inputDto.Value;

            context.Store.Set(key, value, type);
        }
        return context;
    }

    /// <summary>Serialize a <see cref="Pipeline"/> to JSON.</summary>
    /// <param name="pipeline">Pipeline to serialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    public static string Serialize(this Pipeline pipeline, PipelineContext? context = null, JsonSerializerOptions? options = null) {
        return JsonSerializer.Serialize(pipeline.ToDto(context), options);
    }

    /// <summary>Deserialize a <see cref="Pipeline"/> from JSON. Requires a populated <paramref name="registry"/>.</summary>
    /// <param name="json">JSON string produced by <see cref="Serialize"/>.</param>
    /// <param name="registry">Registry containing all node types referenced in the JSON.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    public static (Pipeline pipeline, PipelineContext context) DeserializePipeline(string json, NodeRegistry registry, JsonSerializerOptions? options = null) {
        var dto = JsonSerializer.Deserialize<PipelineDto>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize pipeline JSON.");

        return (dto.FromPipelineDto(registry), dto.FromInputs());
    }
}
