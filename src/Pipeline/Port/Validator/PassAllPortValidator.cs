namespace Shiron.Lib.Pipeline.Port.Validator;

public class PassAllPortValidator : IPortValidator<object?> {
    public bool Validate(object? value) {
        return true;
    }
}
