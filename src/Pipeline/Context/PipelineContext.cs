using System.Collections.Concurrent;

namespace Shiron.Lib.Pipeline.Context;

public class PipelineContext : IPipelineContext {
    private readonly ConcurrentDictionary<Guid, object> _memory = [];

    public void Write(PipelineBuilder.NodeInstance instance, Port port, object value) {
        var connectionId = instance.Mappings[port];
        _memory[connectionId] = value;
    }
    public object Read(PipelineBuilder.NodeInstance node, Port port) {
        var connectionId = node.Mappings[port];
        return _memory[connectionId];
    }
    public void Write(Guid id, object value) {
        _memory[id] = value;
    }
    public object Read(Guid id) {
        return _memory[id];
    }
}
