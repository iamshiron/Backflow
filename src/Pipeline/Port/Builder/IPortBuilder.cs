namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>Fluent factory for creating <see cref="IInputPort{T}"/> and <see cref="IOutputPort{T}"/> instances.</summary>
public interface IPortBuilder<T> {
    /// <summary>Create an input port.</summary>
    IInputPort<T> Input();
    /// <summary>Create an output port.</summary>
    IOutputPort<T> Output();
}
