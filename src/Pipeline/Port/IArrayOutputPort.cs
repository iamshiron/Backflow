namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Typed array output port. Writes <c>T[]</c> values and exposes the element <see cref="ElementType"/>.
/// </summary>
public interface IArrayOutputPort<T> : IOutputPort<T[]> {
    /// <summary>The element type <c>T</c> of the array.</summary>
    Type ElementType { get; }
}
