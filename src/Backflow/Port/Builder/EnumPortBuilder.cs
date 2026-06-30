using Shiron.Backflow.Port.Base;
using Shiron.Backflow.Port.Validator;

namespace Shiron.Backflow.Port.Builder;

/// <summary>Fluent builder for enum ports. Validates that values are defined members of <typeparamref name="T"/>.</summary>
public class EnumPortBuilder<T>(string name) : BasePortBuilder<EnumPortBuilder<T>, T> where T : struct, Enum {
    public override IPortValidator<T> CreateValidator() => new EnumPortValidator<T>(this);

    protected override IInputPort<T> CreateInput() {
        return new InputPort<T>(name, DefaultValue, CreateValidator());
    }
    protected override IOutputPort<T> CreateOutput() {
        return new OutputPort<T>(name);
    }
}
