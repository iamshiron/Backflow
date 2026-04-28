namespace Shiron.Lib.Pipeline.Context;

public interface IPipelineContext {
    void Write(PipelineBuilder.NodeInstance node, Port port, object value);
    object Read(PipelineBuilder.NodeInstance node, Port port);

    void Write(Guid id, object value);
    object Read(Guid id);
}
