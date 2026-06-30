using Shiron.Backflow.Port.Base;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Port.Validator;

/// <summary>Pass-through validator for <c>bool</c> values (no additional constraints).</summary>
public class BoolPortValidator(BoolPortBuilder builder) : BasePortValidator<BoolPortBuilder, bool>(builder) {
    protected override string? ValidateValue(bool value) {
        return null;
    }
}
