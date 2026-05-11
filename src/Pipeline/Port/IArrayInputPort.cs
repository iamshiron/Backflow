using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public interface IArrayInputPortMarker : IPort {
    int MinCount { get; }
    int? MaxCount { get; }
    int? Count { get; }
    bool IsFrozen { get; }
    void SetCount(int count);
}

public interface IArrayInputPort<T> : IInputPort<T[]>, IArrayInputPortMarker {
    T? ReadAt(INodeContext context, int index);
    bool HasValueAt(INodeContext context, int index);
    int GetCount(INodeContext context);
}
