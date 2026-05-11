using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

public class ArrayPortValidator<T>(ArrayPortBuilder<T> builder) : BasePortValidator<ArrayPortBuilder<T>, T[]>(builder) {
    protected override string? ValidateValue(T[] value) {
        if (builder.MinLengthValue.HasValue && value.Length < builder.MinLengthValue) {
            return $"Array length must be at least {builder.MinLengthValue}";
        }

        if (builder.MaxLengthValue.HasValue && value.Length > builder.MaxLengthValue) {
            return $"Array length must be at most {builder.MaxLengthValue}";
        }

        return null;
    }
}
