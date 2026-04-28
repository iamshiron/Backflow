namespace Shiron.Lib.Pipeline.Context;

public class NodeContext(IPipelineContext context, IReadOnlyDictionary<Port, Guid> mappings) : INodeContext {
    public void Write(Port port, object value) {
        context.Write(mappings[port], value);
    }
    public object? Read(Port port) {
        return context.Read(mappings[port]);
    }
}
