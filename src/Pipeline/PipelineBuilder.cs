using Shiron.Lib.Collections;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline;

public class PipelineBuilder(NodeRegistry registry) {
    public readonly record struct NodeInstance(Guid ID, AbstractNode Node, Dictionary<Port, Guid> Mappings);
    public readonly record struct EdgeInstance(NodeInstance SourceNode, Port SourcePort, NodeInstance DestinationNode, Port DestinationPort);

    private readonly DirectedAcyclicGraph<NodeInstance> _graph = new();
    private readonly List<EdgeInstance> _edges = [];

    public NodeInstance AddNode(AbstractNode node) {
        var instance = new NodeInstance(
            Guid.NewGuid(), node,
            node.Ports.ToDictionary(p => p, _ => Guid.NewGuid())
        );

        _graph.AddNode(instance);
        return instance;
    }

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

    public Pipeline Build() {
        return new Pipeline(_graph, [.. _edges]);
    }
}
