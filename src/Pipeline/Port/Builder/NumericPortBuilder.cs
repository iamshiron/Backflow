using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class NumericPortBuilder<T>(string name) : BasePortBuilder<NumericPortBuilder<T>, T> where T : struct, INumber<T> {
    public T? MinValue { get; protected set; }
    public T? MaxValue { get; protected set; }

    public NumericPortBuilder<T> Min(T? minValue) {
        MinValue = minValue;
        return this;
    }
    public NumericPortBuilder<T> Max(T? maxValue) {
        MaxValue = maxValue;
        return this;
    }
    public NumericPortBuilder<T> Range(T? minValue, T? maxValue) {
        return Min(minValue).Max(maxValue);
    }

    public override IPortValidator<T> CreateValidator() => new NumericPortValidator<T>(this);

    protected override IInputPort<T> CreateInput() {
        return new InputPort<T>(name, DefaultValue, CreateValidator());
    }
    protected override IOutputPort<T> CreateOutput() {
        return new OutputPort<T>(name);
    }
}
