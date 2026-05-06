namespace Shiron.Lib.Pipeline.Port;

public interface IPortValidator<in T> {
    string? Validate(T? value);
}
