using Shiron.Lib.Collections;
using Shiron.Backflow.Casting;
using System.Collections.Frozen;

namespace Shiron.Backflow;

/// <summary>
/// Immutable DAG-based pipeline topology. Created by <see cref="PipelineBuilder.Build"/>.
/// </summary>
public readonly record struct Pipeline(
    DirectedAcyclicGraph<PipelineBuilder.NodeInstance> Topology,
    PipelineBuilder.EdgeInstance[] Edges,
    CastRegistry CastRegistry
) {
    private readonly CompiledGraph _compiled = CompiledGraph.Create(Topology, Edges, CastRegistry);

    /// <summary>Gets a defensive graph copy for inspection. Mutations do not change the compiled pipeline.</summary>
    public DirectedAcyclicGraph<PipelineBuilder.NodeInstance> Topology => _compiled.CopyTopology();

    /// <summary>Gets a defensive copy of the immutable connection definitions.</summary>
    public PipelineBuilder.EdgeInstance[] Edges => _compiled.Edges.ToArray();

    /// <summary>Conversion rules snapshotted when the graph is compiled.</summary>
    public CastRegistry CastRegistry => _compiled.Casts;

    internal PipelineBuilder.NodeInstance[][] CompiledLayers => _compiled.Layers;
    internal PipelineBuilder.NodeInstance[] CompiledNodes => _compiled.Nodes;
    internal PipelineBuilder.EdgeInstance[] CompiledEdges => _compiled.Edges;
    internal (Type[] channelTypes, Dictionary<Type, int> sizes) ChannelLayout => _compiled.Layout;

    private sealed class CompiledGraph(
        DirectedAcyclicGraph<PipelineBuilder.NodeInstance> topology,
        PipelineBuilder.EdgeInstance[] edges,
        CastRegistry casts) {
        internal readonly PipelineBuilder.NodeInstance[] Nodes = topology.Nodes.ToArray();
        internal readonly PipelineBuilder.NodeInstance[][] Layers = topology.ToLayers();
        internal readonly PipelineBuilder.EdgeInstance[] Edges = edges;
        internal readonly CastRegistry Casts = casts.Snapshot();
        internal readonly (Type[] channelTypes, Dictionary<Type, int> sizes) Layout = PipelineBuilder.ComputeLayout(topology, edges);

        internal DirectedAcyclicGraph<PipelineBuilder.NodeInstance> CopyTopology() {
            var copy = new DirectedAcyclicGraph<PipelineBuilder.NodeInstance>();
            foreach (var node in Nodes) copy.AddNode(node);
            foreach (var node in Nodes) {
                foreach (var child in topology.GetChildren(node)) copy.AddEdge(node, child);
            }
            return copy;
        }

        internal static CompiledGraph Create(DirectedAcyclicGraph<PipelineBuilder.NodeInstance> source,
            PipelineBuilder.EdgeInstance[] edges, CastRegistry casts) {
            var nodes = source.Nodes.ToDictionary(node => node.ID, node => node with {
                Mappings = node.Mappings.ToFrozenDictionary(),
                ArrayCounts = node.ArrayCounts?.ToFrozenDictionary()
            });
            var graph = new DirectedAcyclicGraph<PipelineBuilder.NodeInstance>();
            foreach (var node in nodes.Values) {
                node.Node.Freeze();
                graph.AddNode(node);
            }
            foreach (var sourceNode in source.Nodes) {
                foreach (var child in source.GetChildren(sourceNode)) graph.AddEdge(nodes[sourceNode.ID], nodes[child.ID]);
            }
            var compiledEdges = edges.Select(edge => edge with {
                SourceNode = nodes[edge.SourceNode.ID],
                DestinationNode = nodes[edge.DestinationNode.ID]
            }).ToArray();
            return new CompiledGraph(graph, compiledEdges, casts);
        }
    }
}
