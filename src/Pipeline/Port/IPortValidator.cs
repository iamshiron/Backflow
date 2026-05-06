namespace Shiron.Lib.Pipeline.Port;

public interface IPortValidator<in T> {
    bool Validate(T? value);
}
