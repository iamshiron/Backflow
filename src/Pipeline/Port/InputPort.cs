using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public class InputPort<T>(string name, IPortValidator<T> validator) : Port(name), IInputPort<T> {
    public Guid ID { get; } = Guid.NewGuid();

    public T? Read(INodeContext context) {
        return context.Read(this) is T? ? (T?) context.Read(this) : default;
    }
    public bool TryRead(INodeContext context, out T? value) {
        value = context.Read(this) is T? ? (T?) context.Read(this) : default;
        return value != null;
    }
    public bool HasValue(INodeContext context) {
        return context.Read(this) is T?;
    }
}
