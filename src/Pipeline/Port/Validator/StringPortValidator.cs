using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

/// <summary>Validates string values against length and multiline constraints from <see cref="StringPortBuilder"/>.</summary>
public class StringPortValidator(StringPortBuilder builder) : BasePortValidator<StringPortBuilder, string>(builder) {
    protected override string? ValidateValue(string value) {
        if (builder.MaxLengthValue.HasValue && value.Length > builder.MaxLengthValue.Value) {
            return $"Length {value.Length} exceeds maximum {builder.MaxLengthValue.Value}.";
        }
        if (builder.MinLengthValue.HasValue && value.Length < builder.MinLengthValue.Value) {
            return $"Length {value.Length} is below minimum {builder.MinLengthValue.Value}.";
        }
        if (builder.AllowMultiline.HasValue && !builder.AllowMultiline.Value && value.Contains(Environment.NewLine)) {
            return $"String contains newline characters, but multiline is not allowed.";
        }

        return null;
    }
}
