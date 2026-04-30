namespace Shiron.Lib.Pipeline.Port;

public interface IPortValidator<T> {
    bool Validate(T? value);
}
