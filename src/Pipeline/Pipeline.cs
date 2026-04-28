using Shiron.Lib.Collections;

namespace Shiron.Lib.Pipeline;

public readonly record struct Pipeline(
    DirectedAcyclicGraph<PipelineBuilder.NodeInstance> Topology,
    PipelineBuilder.EdgeInstance[] Edges
);
