using Shiron.Backflow.Port.Base;
using Shiron.Backflow.Port.Validator;

namespace Shiron.Backflow.Port.Builder;

/// <summary>Fluent builder for untyped (<c>object?</c>) ports.</summary>
public class AnyPortBuilder(string name) : BasePortBuilder<AnyPortBuilder, object?> {
    public override IPortValidator<object?> CreateValidator() => new PassAllPortValidator<object?>();

    protected override IInputPort<object?> CreateInput() {
        return new InputPort<object?>(name, null, CreateValidator());
    }
    protected override IOutputPort<object?> CreateOutput() {
        return new OutputPort<object?>(name);
    }
}
