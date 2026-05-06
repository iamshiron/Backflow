using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline.Port;

public class InputPort<T>(string name, T? defaultValue, IPortValidator<T> validator) : Port(name), IInputPort<T> {
    public T? Read(INodeContext context) {
        var value = context.Read<T>(this) ?? defaultValue;
        FailFast(value);
        return value;
    }
    public object? ReadAny(INodeContext context) {
        var value = context.ReadAny(this);
        FailFast((T?) value);
        return value;
    }

    public bool TryRead(INodeContext context, out T? value) {
        var has = context.Has<T>(this);
        if (!has) {
            value = defaultValue;
            return false;
        }

        value = context.Read<T>(this);
        FailFast(value);
        return true;
    }
    public bool HasValue(INodeContext context) {
        return context.Has<T>(this);
    }

    private void FailFast(T? value) {
        var error = validator.Validate(value);
        if (error is not null) {
            throw new PortValidationException(Name, value, error);
        }
    }
}
