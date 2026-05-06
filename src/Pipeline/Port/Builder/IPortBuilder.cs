namespace Shiron.Lib.Pipeline.Port.Builder;

public interface IPortBuilder<T> {
    IInputPort<T> Input();
    IOutputPort<T> Output();
}
