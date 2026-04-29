using Shiron.Lib.Collections;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Immutable DAG-based pipeline topology. Created by <see cref="PipelineBuilder.Build"/>.
/// </summary>
public readonly record struct Pipeline(
    DirectedAcyclicGraph<PipelineBuilder.NodeInstance> Topology,
    PipelineBuilder.EdgeInstance[] Edges
);
