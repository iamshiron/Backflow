using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

/// <summary>Validates array length/count constraints from <see cref="ArrayPortBuilder{T}"/>.</summary>
public class ArrayPortValidator<T>(ArrayPortBuilder<T> builder) : BasePortValidator<ArrayPortBuilder<T>, T[]>(builder) {
    protected override string? ValidateValue(T[] value) {
        if (builder.MinLengthValue.HasValue && value.Length < builder.MinLengthValue) {
            return $"Array length must be at least {builder.MinLengthValue}";
        }

        if (builder.MaxLengthValue.HasValue && value.Length > builder.MaxLengthValue) {
            return $"Array length must be at most {builder.MaxLengthValue}";
        }

        if (value.Length < builder.MinCountValue) {
            return $"Array count must be at least {builder.MinCountValue}";
        }

        if (builder.MaxCountValue.HasValue && value.Length > builder.MaxCountValue) {
            return $"Array count must be at most {builder.MaxCountValue}";
        }

        return null;
    }
}
