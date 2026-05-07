using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Validator;

public class BoolPortValidator(BoolPortBuilder builder) : BasePortValidator<BoolPortBuilder, bool>(builder) {
    protected override string? ValidateValue(bool value) {
        return null;
    }
}
