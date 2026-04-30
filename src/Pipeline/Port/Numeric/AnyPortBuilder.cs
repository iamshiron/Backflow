namespace Shiron.Lib.Pipeline.Port.Numeric;

public class AnyPortBuilder(string name) {
    public InputPort<object?> Input() {
        return new InputPort<object?>(name, new PassAllPortValidator());
    }
    public OutputPort<object?> Output() {
        return new OutputPort<object?>(name);
    }
}
