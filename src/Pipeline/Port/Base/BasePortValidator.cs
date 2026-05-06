namespace Shiron.Lib.Pipeline.Port.Base;

public abstract class BasePortValidator<TBuilder, TValue>(BasePortBuilder<TBuilder, TValue> builder)
    : IPortValidator<TValue> where TBuilder : BasePortBuilder<TBuilder, TValue> {
    public string? Validate(TValue? value) {
        if (value is null) {
            return builder.IsNullable ? null : "Value is null but the port is not nullable.";
        }

        return ValidateValue(value);
    }

    protected abstract string? ValidateValue(TValue value);
}
