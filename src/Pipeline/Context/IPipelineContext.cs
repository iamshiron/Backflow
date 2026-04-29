namespace Shiron.Lib.Pipeline.Context;

/// <summary>
/// Global shared memory for a pipeline execution. Passed to <see cref="PipelineExecutor.Execute"/>
/// and <see cref="PipelineExecutor.ExecuteAsync"/>.
/// </summary>
public interface IPipelineContext {
    /// <summary>Write via node + port (resolves to the shared channel GUID).</summary>
    /// <param name="node">Node instance that owns the port.</param>
    /// <param name="port">Target port on the node.</param>
    /// <param name="value">Value to write.</param>
    void Write(PipelineBuilder.NodeInstance node, Port port, object value);

    /// <summary>Read via node + port (resolves to the shared channel GUID).</summary>
    /// <param name="node">Node instance that owns the port.</param>
    /// <param name="port">Source port on the node.</param>
    object Read(PipelineBuilder.NodeInstance node, Port port);

    /// <summary>Write directly by channel GUID.</summary>
    /// <param name="id">Channel GUID.</param>
    /// <param name="value">Value to write.</param>
    void Write(Guid id, object value);

    /// <summary>Read directly by channel GUID.</summary>
    /// <param name="id">Channel GUID.</param>
    object Read(Guid id);
}
