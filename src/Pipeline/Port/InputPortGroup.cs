using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline.Port;

public class InputPortGroup<T>(
    string name,
    T? defaultValue,
    IPortValidator<T> validator,
    int minCount,
    int? maxCount
) : Port(name), IInputPortGroup<T> {
    public override Type PortType { get; protected set; } = typeof(T);
    public Type ElementType => typeof(T);
    public int MinCount { get; } = minCount;
    public int? MaxCount { get; } = maxCount;

    public T? Read(INodeContext context, int index) {
        var value = context.HasGroup<T>(this, index)
            ? context.ReadGroup<T>(this, index)
            : defaultValue;
        FailFast(value);
        return value;
    }

    public IReadOnlyList<T?> ReadAll(INodeContext context) {
        var count = context.GetGroupCount(this);
        var result = new T?[count];
        for (var i = 0; i < count; i++) {
            result[i] = Read(context, i);
        }
        return result;
    }

    public bool HasValue(INodeContext context, int index) {
        return context.HasGroup<T>(this, index);
    }

    public int Count(INodeContext context) {
        return context.GetGroupCount(this);
    }

    private void FailFast(T? value) {
        var error = validator.Validate(value);
        if (error is not null) {
            throw new PortValidationException(Name, value, error);
        }
    }
}
