using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

/// <summary>Marker interface for array input ports, providing count constraints and freeze state.</summary>
public interface IArrayInputPortMarker : IPort {
    /// <summary>Minimum number of elements required.</summary>
    int MinCount { get; }
    /// <summary>Maximum number of elements allowed, or <c>null</c> for unbounded.</summary>
    int? MaxCount { get; }
    /// <summary>Fixed element count once the port is frozen, or <c>null</c> if unfrozen.</summary>
    int? Count { get; }
    /// <summary>Whether <see cref="SetCount"/> has been called.</summary>
    bool IsFrozen { get; }
    /// <summary>Freeze the array to a fixed element count.</summary>
    void SetCount(int count);
}

/// <summary>
/// Typed array input port. Reads <c>T[]</c> values and supports indexed element access.
/// </summary>
public interface IArrayInputPort<T> : IInputPort<T[]>, IArrayInputPortMarker {
    /// <summary>Read the element at the given index, returning the element default if out of range.</summary>
    T? ReadAt(INodeContext context, int index);
    /// <summary>Whether an element exists at the given index.</summary>
    bool HasValueAt(INodeContext context, int index);
    /// <summary>Get the current element count (frozen count or actual array length).</summary>
    int GetCount(INodeContext context);
}
