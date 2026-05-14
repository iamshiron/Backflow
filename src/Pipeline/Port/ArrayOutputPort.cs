namespace Shiron.Lib.Pipeline.Port;

public class ArrayOutputPort<T>(string name) : OutputPort<T[]>(name), IArrayOutputPort<T> {
    public Type ElementType { get; } = typeof(T);
}
