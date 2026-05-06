using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class AnyPortBuilder(string name) : IPortBuilder<object?> {
    public IInputPort<object?> Input() {
        return new InputPort<object?>(name, new PassAllPortValidator());
    }
    public IOutputPort<object?> Output() {
        return new OutputPort<object?>(name);
    }
}
