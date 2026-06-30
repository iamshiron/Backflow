using Shiron.Backflow.Port.Base;
using Shiron.Backflow.Port.Validator;

namespace Shiron.Backflow.Port.Builder;

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
