using Shiron.Lib.Pipeline.Port.Numeric;

namespace Shiron.Lib.Pipeline.Port.Any;

public class AnyPortBuilder(string name) : IPortBuilder<object?> {
    public IInputPort<object?> Input() {
        return new InputPort<object?>(name, new PassAllPortValidator());
    }
    public IOutputPort<object?> Output() {
        return new OutputPort<object?>(name);
    }
}
