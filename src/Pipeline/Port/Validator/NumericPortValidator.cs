using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

public class NumericPortValidator<T>(NumericPortBuilder<T> builder)
    : BasePortValidator<NumericPortBuilder<T>, T>(builder) where T : INumber<T> {
    protected override string? ValidateValue(T value) {
        if (builder.MinValue is not null && value < builder.MinValue) {
            return $"Value {value} is below minimum {builder.MinValue}.";
        }
        if (builder.MaxValue is not null && value > builder.MaxValue) {
            return $"Value {value} exceeds maximum {builder.MaxValue}.";
        }

        return null;
    }
}
