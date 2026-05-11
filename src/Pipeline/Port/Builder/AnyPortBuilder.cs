using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class AnyPortBuilder(string name) : BasePortBuilder<AnyPortBuilder, object?> {
    public override IPortValidator<object?> CreateValidator() => new PassAllPortValidator<object?>();

    protected override IInputPort<object?> CreateInput() {
        return new InputPort<object?>(name, null, CreateValidator());
    }
    protected override IOutputPort<object?> CreateOutput() {
        return new OutputPort<object?>(name);
    }
}
