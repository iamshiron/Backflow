using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

public class InputPort<T>(string name, IPortValidator<T> validator) : Port(name), IInputPort<T> {
    public T? Read(INodeContext context) {
        return context.Read<T>(this);
    }
    public object? ReadAny(INodeContext context) {
        return context.ReadAny(this);
    }

    public bool TryRead(INodeContext context, out T? value) {
        var has = context.Has<T>(this);
        if (!has) {
            value = default;
            return false;
        }

        value = context.Read<T>(this);
        return true;
    }
    public bool HasValue(INodeContext context) {
        return context.Has<T>(this);
    }
}
