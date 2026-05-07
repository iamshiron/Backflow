using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

public class NumericPortValidator<T>(NumericPortBuilder<T> builder)
    : BasePortValidator<NumericPortBuilder<T>, T>(builder) where T : struct, INumber<T> {
    protected override string? ValidateValue(T value) {
        if (builder.MinValue.HasValue && value < builder.MinValue.Value) {
            return $"Value {value} is below minimum {builder.MinValue}.";
        }
        if (builder.MaxValue.HasValue && value > builder.MaxValue.Value) {
            return $"Value {value} exceeds maximum {builder.MaxValue}.";
        }

        return null;
    }
}
