namespace Shiron.Lib.Pipeline.Port.Validator;

public class PassAllPortValidator<T> : IPortValidator<T> {
    public string? Validate(T? value) {
        return null;
    }
}
