namespace Shiron.Lib.Pipeline.Port;

public interface IArrayOutputPort<T> : IOutputPort<T[]> {
    Type ElementType { get; }
}
