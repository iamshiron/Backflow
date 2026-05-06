using System.Numerics;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class NumericPortBuilder<T>(string name) : IPortBuilder<T> where T : INumber<T> {
    public bool IsRequired { get; private set; } = true;
    public bool NotNullable { get; private set; } = true;
    public T? DefaultValue { get; private set; }
    public T? MaxValue { get; private set; }
    public T? MinValue { get; private set; }

    public NumericPortBuilder<T> Optional(bool optional = true) {
        IsRequired = !optional;
        return this;
    }
    public NumericPortBuilder<T> Nullable(bool nullable = true) {
        NotNullable = !nullable;
        return this;
    }
    public NumericPortBuilder<T> Default(T? value) {
        DefaultValue = value;
        return this;
    }
    public NumericPortBuilder<T> Max(T? value) {
        MaxValue = value;
        return this;
    }
    public NumericPortBuilder<T> Min(T? value) {
        MinValue = value;
        return this;
    }
    public NumericPortBuilder<T> Range(T? min, T? max) {
        return Min(min).Max(max);
    }

    public IInputPort<T> Input() {
        return new InputPort<T>(name, new NumericPortValidator<T>(this));
    }
    public IOutputPort<T> Output() {
        return new OutputPort<T>(name);
    }
}
