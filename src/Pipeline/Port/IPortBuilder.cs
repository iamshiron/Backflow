namespace Shiron.Lib.Pipeline.Port;

public interface IPortBuilder<T> {
    IInputPort<T> Input();
    IOutputPort<T> Output();
}
