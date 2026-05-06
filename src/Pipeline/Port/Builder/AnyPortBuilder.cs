using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class AnyPortBuilder(string name) : BasePortBuilder<AnyPortBuilder, object?> {
    protected override IInputPort<object?> CreateInput() {
        return new InputPort<object?>(name, null, new PassAllPortValidator());
    }
    protected override IOutputPort<object?> CreateOutput() {
        return new OutputPort<object?>(name);
    }
}
