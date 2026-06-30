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
);
