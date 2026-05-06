using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

public class StringPortValidator(StringPortBuilder builder) : BasePortValidator<StringPortBuilder, string>(builder) {
    protected override string? ValidateValue(string value) {
        if (builder.MaxLengthValue.HasValue && value.Length > builder.MaxLengthValue.Value) {
            return $"Length {value.Length} exceeds maximum {builder.MaxLengthValue.Value}.";
        }
        if (builder.MinLengthValue.HasValue && value.Length < builder.MinLengthValue.Value) {
            return $"Length {value.Length} is below minimum {builder.MinLengthValue.Value}.";
        }
        return null;
    }
}
