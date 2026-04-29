using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Fluent builder for constructing <see cref="Pipeline"/> instances.
/// Nodes are added, then connected via ports; <see cref="Build"/> produces the final topology.
/// </summary>
/// <param name="registry">Node registry used for type lookups during deserialization.</param>
public class PipelineBuilder(NodeRegistry registry) {
    /// <summary>A node instance within the pipeline, holding port-to-channel mappings.</summary>
    public readonly record struct NodeInstance(Guid ID, AbstractNode Node, Dictionary<Port, Guid> Mappings);

    /// <summary>A directed edge between two node ports.</summary>
    public readonly record struct EdgeInstance(NodeInstance SourceNode, Port SourcePort, NodeInstance DestinationNode, Port DestinationPort);

    private readonly DirectedAcyclicGraph<NodeInstance> _graph = new();
    private readonly List<EdgeInstance> _edges = [];

    /// <summary>Add a node to the pipeline and return its instance handle.</summary>
    /// <param name="node">Node to add.</param>
    public NodeInstance AddNode(AbstractNode node) {
        var instance = new NodeInstance(
            Guid.NewGuid(), node,
            node.Ports.ToDictionary(p => p, _ => Guid.NewGuid())
        );

        _graph.AddNode(instance);
        return instance;
    }

    /// <summary>
    /// Connect <paramref name="sourcePort"/> on <paramref name="source"/> to <paramref name="destinationPort"/> on <paramref name="destination"/>.
    /// Throws if either port is invalid or if the connection would introduce a cycle.
    /// </summary>
    /// <param name="source">Source node instance.</param>
    /// <param name="sourcePort">Output port on the source node.</param>
    /// <param name="destination">Destination node instance.</param>
    /// <param name="destinationPort">Input port on the destination node.</param>
    /// <exception cref="InvalidPortException">Port does not belong to the specified node.</exception>
    /// <exception cref="PipelineCycleException">Connection would introduce a cycle.</exception>
    public void AddConnection(NodeInstance source, Port sourcePort, NodeInstance destination, Port destinationPort) {
        if (!source.Node.Ports.Contains(sourcePort))
            throw new InvalidPortException(sourcePort, source.Node.GetType());

        if (!destination.Node.Ports.Contains(destinationPort))
            throw new InvalidPortException(destinationPort, destination.Node.GetType());

        try {
            _graph.AddEdge(source, destination);
        } catch (InvalidOperationException) {
            throw new PipelineCycleException(source, destination);
        }

        _edges.Add(new EdgeInstance(source, sourcePort, destination, destinationPort));
        destination.Mappings[destinationPort] = source.Mappings[sourcePort];
    }

    /// <summary>Build and return the immutable <see cref="Pipeline"/>.</summary>
    public Pipeline Build() {
        return new Pipeline(_graph, [.. _edges]);
    }
}
