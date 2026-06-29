using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

/// <summary>Marker interface for array input ports, providing count constraints.</summary>
public interface IArrayInputPortMarker : IPort {
    /// <summary>Minimum number of elements required.</summary>
    int MinCount { get; }
    /// <summary>Maximum number of elements allowed, or <c>null</c> for unbounded.</summary>
    int? MaxCount { get; }

    /// <summary>
    /// Validate that <paramref name="count"/> satisfies the <see cref="MinCount"/> and
    /// <see cref="MaxCount"/> constraints. Throws on violation.
    /// </summary>
    /// <param name="count">The element count to validate.</param>
    void ValidateCount(int count);
}

/// <summary>
/// Typed array input port. Reads <c>T[]</c> values and supports indexed element access.
/// </summary>
public interface IArrayInputPort<T> : IInputPort<T[]>, IArrayInputPortMarker {
    /// <summary>Read the element at the given index, returning the element default if out of range.</summary>
    T? ReadAt(INodeContext context, int index);
    /// <summary>Whether an element exists at the given index.</summary>
    bool HasValueAt(INodeContext context, int index);
    /// <summary>Get the current element count (actual array length, or 0 if no value).</summary>
    int GetCount(INodeContext context);
    /// <summary>
    /// Whether the element at <paramref name="index"/> was supplied (connected/written)
    /// rather than being a default fill.
    /// </summary>
    bool IsSuppliedAt(INodeContext context, int index);
}
