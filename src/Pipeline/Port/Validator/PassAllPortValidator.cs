namespace Shiron.Lib.Pipeline.Port.Validator;

public class PassAllPortValidator : IPortValidator<object?> {
    public string? Validate(object? value) {
        return null;
    }
}
