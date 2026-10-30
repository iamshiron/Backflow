using System.Text.Json;
using System.Text.Json.Schema;
using Shiron.Backflow.Context;
using Shiron.Backflow.Exceptions;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;
using Shiron.Backflow.Serialization;
using Shiron.Lib.Collections;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>Validates untrusted topology against a startup-only whitelist, without executing nodes.</summary>
public sealed class BackflowGraphService {
    private readonly Dictionary<string, BackflowCatalogEntry> _nodes;
    private readonly NodeRegistry _registry = new();
    private readonly BackflowOptions _options;

    internal BackflowGraphService(BackflowCatalogBuilder catalog, BackflowOptions options) {
        _nodes = catalog.Snapshot();
        _options = options;
        foreach (var node in _nodes.Values.Select(entry => entry.Node).OfType<AbstractNode>().Distinct())
            _registry.Register(node);
    }

    /// <summary>Derive catalog schemas from host-declared port CLR types, using the configured serializer.</summary>
    public BackflowCatalogDescriptor GetCatalog() => new(_nodes.Select(entry =>
        new BackflowNodeDescriptor(entry.Key, entry.Value.Ports.Select(port =>
            new BackflowPortDescriptor(port.Name, port.Direction,
                port.Type.FullName ?? port.Type.Name,
                _options.ValueSerializerOptions.GetJsonSchemaAsNode(port.Type), port.Required,
                port.MinimumCount, port.MaximumCount)).ToArray(), entry.Value.Node is not null,
            entry.Value.ConfigurationType is { } type ? _options.ValueSerializerOptions.GetJsonSchemaAsNode(type) : null)).ToArray());

    /// <summary>Validate the graph without side effects, instantiation or execution.</summary>
    public BackflowValidationResult Validate(BackflowGraph graph, CancellationToken cancellationToken = default) {
        TryCompile(graph, out _, out var result, cancellationToken);
        return result;
    }

    /// <summary>Compile declared ports and DAG order without requiring executable nodes or changing the host document.</summary>
    public bool TryCompile(BackflowGraph graph, out BackflowCompiledGraph? compiled, out BackflowValidationResult result,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        compiled = null;
        var error = CheckGraph(graph, out var orderedNodeIds, cancellationToken);
        if (error is not null) {
            result = new(false, [error]);
            return false;
        }
        compiled = new(graph, orderedNodeIds);
        result = new(true, []);
        return true;
    }

    /// <summary>Build a request-local pipeline while preserving client node IDs.</summary>
    public bool TryBuild(BackflowGraph graph, out Pipeline? pipeline, out BackflowValidationResult result,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        pipeline = null;
        if (!TryCompile(graph, out _, out result, cancellationToken)) return false;
        foreach (var node in graph.Nodes) {
            if (_nodes[node.CatalogId].Node is null) {
                result = new(false, [new(BackflowErrorCode.NotExecutable, "The node has a declaration but no executable implementation.", node.Id)]);
                return false;
            }
        }

        var definition = new PipelineDefinitionDto(graph.Nodes.Select(node =>
            new NodeInstanceDto(node.Id, _nodes[node.CatalogId].Node!.GetType().FullName!)).ToArray(),
            graph.Edges.Select(edge => new EdgeDto(edge.SourceNodeId, edge.SourcePort,
                edge.DestinationNodeId, edge.DestinationPort, edge.DestinationIndex)).ToArray());
        try {
            pipeline = definition.FromDefinitionDto(_registry);
            cancellationToken.ThrowIfCancellationRequested();
            result = new(true, []);
            return true;
        } catch (PipelineDeserializationException) {
            result = new(false, [new(BackflowErrorCode.InvalidGraph, "The graph contains a cycle or invalid array cardinality.")]);
            return false;
        }
    }

    /// <summary>Decode literals solely as the declared input port types for host scheduling.</summary>
    public bool TryPrepareExecution(BackflowExecutionSubmission submission, out BackflowExecutionRequest? request,
        out BackflowValidationResult result, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(submission);
        request = null;
        if (!TryBuild(submission.Graph, out var pipeline, out result, cancellationToken)) return false;
        var error = CheckInputs(submission, pipeline!.Value, out var context, cancellationToken);
        if (error is not null) {
            result = new(false, [error]);
            return false;
        }
        request = new(submission.Graph, pipeline.Value, context!);
        return true;
    }

    private BackflowError? CheckGraph(BackflowGraph? graph, out string[] orderedNodeIds, CancellationToken cancellationToken) {
        orderedNodeIds = [];
        if (graph?.Nodes is null || graph.Edges is null)
            return new(BackflowErrorCode.InvalidRequest, "Nodes and edges arrays are required.");
        if (graph.Nodes.Length > _options.MaximumNodes || graph.Edges.Length > _options.MaximumEdges)
            return new(BackflowErrorCode.LimitExceeded, "The graph exceeds the configured node or edge limit.");
        var instances = new Dictionary<string, BackflowCatalogEntry>(StringComparer.Ordinal);
        var topology = new DirectedAcyclicGraph<string>();
        foreach (var node in graph.Nodes) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is null || !ValidIdentifier(node.Id) || !ValidIdentifier(node.CatalogId))
                return new(BackflowErrorCode.InvalidRequest, "Node identifiers are missing or too long.");
            if (!_nodes.TryGetValue(node.CatalogId, out var registered))
                return new(BackflowErrorCode.UnknownNode, "The node is not in the published catalog.", node.Id);
            if (!instances.TryAdd(node.Id, registered))
                return new(BackflowErrorCode.InvalidGraph, "Node identifiers must be unique.", node.Id);
            topology.AddNode(node.Id);
        }

        var targets = new HashSet<(string Node, string Port, int? Index)>();
        for (var i = 0; i < graph.Edges.Length; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = graph.Edges[i];
            if (edge is null || !ValidIdentifier(edge.SourceNodeId) || !ValidIdentifier(edge.DestinationNodeId) ||
                !ValidIdentifier(edge.SourcePort) || !ValidIdentifier(edge.DestinationPort))
                return new(BackflowErrorCode.InvalidRequest, "Edge identifiers are missing or too long.", EdgeIndex: i);
            if (!instances.TryGetValue(edge.SourceNodeId, out var source) ||
                !instances.TryGetValue(edge.DestinationNodeId, out var destination))
                return new(BackflowErrorCode.InvalidGraph, "An edge references an unknown node.", EdgeIndex: i);
            var output = source.Ports.FirstOrDefault(port => port.Name == edge.SourcePort && port.Direction == BackflowPortDirection.Output);
            var input = destination.Ports.FirstOrDefault(port => port.Name == edge.DestinationPort && port.Direction == BackflowPortDirection.Input);
            if (output is null || input is null)
                return new(BackflowErrorCode.InvalidPort, "Connections must join a declared output to a declared input.", EdgeIndex: i);
            var destinationType = input.Type;
            if (edge.DestinationIndex is { } index) {
                if (index < 0 || index >= _options.MaximumArrayLength)
                    return new(BackflowErrorCode.LimitExceeded, "The array index exceeds the configured bound.", EdgeIndex: i);
                if (!input.MinimumCount.HasValue || !destinationType.IsArray)
                    return new(BackflowErrorCode.InvalidPort, "Indexed connections require an array input.", EdgeIndex: i);
                destinationType = destinationType.GetElementType()!;
            }
            if (output.Type != destinationType)
                return new(BackflowErrorCode.InvalidPort, "Connected port types must match exactly.", EdgeIndex: i);
            if (!targets.Add((edge.DestinationNodeId, edge.DestinationPort, edge.DestinationIndex)))
                return new(BackflowErrorCode.InvalidPort, "An input slot cannot have multiple writers.", EdgeIndex: i);
            try {
                topology.AddEdge(edge.SourceNodeId, edge.DestinationNodeId);
            } catch (InvalidOperationException) {
                return new(BackflowErrorCode.InvalidGraph, "The graph contains a cycle.", EdgeIndex: i);
            }
        }
        foreach (var group in targets.GroupBy(target => (target.Node, target.Port))) {
            if (group.Count() > 1 && group.Any(target => target.Index is null))
                return new(BackflowErrorCode.InvalidPort, "Whole-array and indexed connections cannot share an input.");
            if (group.All(target => target.Index.HasValue)) {
                var count = group.Max(target => target.Index!.Value) + 1;
                var port = instances[group.Key.Node].Ports.Single(port => port.Name == group.Key.Port);
                if (count < port.MinimumCount || count > port.MaximumCount)
                    return new(BackflowErrorCode.InvalidPort, "Indexed connections violate array cardinality.", group.Key.Node);
            }
        }
        orderedNodeIds = topology.TopologicalSort().ToArray();
        return null;
    }

    private BackflowError? CheckInputs(BackflowExecutionSubmission submission, Pipeline pipeline,
        out ArrayPipelineContext? context, CancellationToken cancellationToken) {
        context = null;
        if (submission.Inputs is null)
            return new(BackflowErrorCode.InvalidRequest, "An inputs array is required.");
        if (submission.Inputs.Length > _options.MaximumInputs)
            return new(BackflowErrorCode.LimitExceeded, "The execution exceeds the configured literal limit.");
        var instances = pipeline.Topology.Nodes.ToDictionary(node => node.ID, StringComparer.Ordinal);
        var connected = submission.Graph.Edges.Select(edge => (edge.DestinationNodeId, edge.DestinationPort)).ToHashSet();
        var supplied = new HashSet<(string Node, string Port)>();
        var prepared = ArrayPipelineContext.ForPipeline(pipeline);
        foreach (var input in submission.Inputs) {
            cancellationToken.ThrowIfCancellationRequested();
            if (input is null || !ValidIdentifier(input.NodeId) || !ValidIdentifier(input.PortName) ||
                !instances.TryGetValue(input.NodeId, out var instance))
                return new(BackflowErrorCode.InvalidInput, "An input references an unknown node or invalid identifier.");
            var port = instance.Node.Inputs.FirstOrDefault(port => port.Name == input.PortName);
            if (port is null || connected.Contains((input.NodeId, input.PortName)) || !supplied.Add((input.NodeId, input.PortName)))
                return new(BackflowErrorCode.InvalidInput, "Literals must target unique, unconnected input ports.", input.NodeId);
            try {
                var value = input.Value.Deserialize(port.PortType, _options.ValueSerializerOptions);
                if (value is Array array && array.Length > _options.MaximumArrayLength)
                    return new(BackflowErrorCode.LimitExceeded, "The literal array exceeds the configured bound.", input.NodeId);
                if (value is null && port.PortType.IsValueType && Nullable.GetUnderlyingType(port.PortType) is null)
                    return new(BackflowErrorCode.InvalidInput, "A nonnullable input cannot be null.", input.NodeId);
                if (port is IArrayInputPortMarker arrayPort && value is Array arrayValue)
                    arrayPort.ValidateCount(arrayValue.Length);
                prepared.Write(instance.Mappings[port], value, port.PortType);
            } catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException or PortValidationException) {
                return new(BackflowErrorCode.InvalidInput, "The literal does not satisfy the declared input type or cardinality.", input.NodeId);
            }
        }
        context = prepared;
        return null;
    }

    private bool ValidIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= _options.MaximumIdentifierLength;
}
