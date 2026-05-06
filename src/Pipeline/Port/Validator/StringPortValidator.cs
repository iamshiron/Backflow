using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

public class StringPortValidator(StringPortBuilder builder) : BasePortValidator<StringPortBuilder, string>(builder) {
    protected override bool ValidateValue(string value) {
        if (builder.MaxLengthValue.HasValue && value.Length > builder.MaxLengthValue.Value) {
            return false;
        }
        if (builder.MinLengthValue.HasValue && value.Length < builder.MinLengthValue.Value) {
            return false;
        }
        return true;
    }
}
