using System.Numerics;

namespace Shiron.Lib.Pipeline.Port.Numeric;

public class NumericPortValidator<T>(NumericPortBuilder<T> builder) : IPortValidator<T> where T : INumber<T> {
    public bool Validate(T? value) {
        if (value is null) {
            return !builder.NotNullable;
        }

        if (builder.MinValue != null) {
            if (value < builder.MinValue) return false;
        }

        if (builder.MaxValue != null) {
            if (value > builder.MaxValue) return false;
        }

        return true;
    }
}
