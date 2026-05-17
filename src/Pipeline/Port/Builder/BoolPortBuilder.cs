using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>Fluent builder for <c>bool</c> ports.</summary>
public class BoolPortBuilder(string name) : BasePortBuilder<BoolPortBuilder, bool> {
    public override IPortValidator<bool> CreateValidator() => new BoolPortValidator(this);

    protected override IInputPort<bool> CreateInput() {
        return new InputPort<bool>(name, DefaultValue, CreateValidator());
    }
    protected override IOutputPort<bool> CreateOutput() {
        return new OutputPort<bool>(name);
    }
}
