namespace Shiron.Lib.Pipeline.Port.Base;

public abstract class BasePortValidator<TBuilder, TValue>(BasePortBuilder<TBuilder, TValue> builder)
    : IPortValidator<TValue> where TBuilder : BasePortBuilder<TBuilder, TValue> {
    public bool Validate(TValue? value) {
        if (value is null) {
            return builder.IsNullable;
        }

        return ValidateValue(value);
    }

    protected abstract bool ValidateValue(TValue value);
}
