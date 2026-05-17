namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Concrete <see cref="IArrayOutputPort{T}"/> implementation.
/// </summary>
public class ArrayOutputPort<T>(string name) : OutputPort<T[]>(name), IArrayOutputPort<T> {
    public Type ElementType { get; } = typeof(T);
}
