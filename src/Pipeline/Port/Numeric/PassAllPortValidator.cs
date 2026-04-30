namespace Shiron.Lib.Pipeline.Port.Numeric;

public class PassAllPortValidator : IPortValidator<object?> {
    public bool Validate(object? value) {
        return true;
    }
}
