namespace Shiron.Lib.Pipeline.Port.Validator;

/// <summary>Pass-through validator that accepts all values.</summary>
public class PassAllPortValidator<T> : IPortValidator<T> {
    public string? Validate(T? value) {
        return null;
    }
}
