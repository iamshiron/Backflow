using Shiron.Lib.Collections;
using Shiron.Backflow.Casting;

namespace Shiron.Backflow;

/// <summary>
/// Immutable DAG-based pipeline topology. Created by <see cref="PipelineBuilder.Build"/>.
/// </summary>
public readonly record struct Pipeline(
    DirectedAcyclicGraph<PipelineBuilder.NodeInstance> Topology,
    PipelineBuilder.EdgeInstance[] Edges,
    CastRegistry CastRegistry
) {
    /// <summary>Conversion rules snapshotted when the graph is compiled.</summary>
    public CastRegistry CastRegistry { get; } = CastRegistry.Snapshot();

    internal (Type[] channelTypes, Dictionary<Type, int> sizes) ChannelLayout { get; } =
        PipelineBuilder.ComputeLayout(Topology, Edges);
}
