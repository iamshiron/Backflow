namespace Shiron.Lib.Pipeline.Port.Base;

/// <summary>
/// Base validator that handles null-checking against the builder's <see cref="BasePortBuilder{TBuilder,TValue}.IsNullable"/> flag.
/// Subclasses override <see cref="ValidateValue"/> for type-specific checks.
/// </summary>
public abstract class BasePortValidator<TBuilder, TValue>(BasePortBuilder<TBuilder, TValue> builder)
    : IPortValidator<TValue> where TBuilder : BasePortBuilder<TBuilder, TValue> {
    public string? Validate(TValue? value) {
        if (value is null) {
            return builder.IsNullable ? null : "Value is null but the port is not nullable.";
        }

        return ValidateValue(value);
    }

    /// <summary>Validate a non-null value. Return <c>null</c> if valid, or an error description.</summary>
    protected abstract string? ValidateValue(TValue value);
}
