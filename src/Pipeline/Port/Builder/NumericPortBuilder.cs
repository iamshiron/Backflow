using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>
/// Fluent builder for numeric ports (<c>int</c>, <c>float</c>, <c>double</c>, etc.).
/// Supports <see cref="Min"/>, <see cref="Max"/>, and <see cref="Range"/> constraints.
/// </summary>
public class NumericPortBuilder<T>(string name) : BasePortBuilder<NumericPortBuilder<T>, T> where T : struct, INumber<T> {
    /// <summary>Minimum allowed value, or <c>null</c> for no lower bound.</summary>
    public T? MinValue { get; protected set; }
    /// <summary>Maximum allowed value, or <c>null</c> for no upper bound.</summary>
    public T? MaxValue { get; protected set; }

    /// <summary>Set the minimum allowed value.</summary>
    public NumericPortBuilder<T> Min(T? minValue) {
        MinValue = minValue;
        return this;
    }
    /// <summary>Set the maximum allowed value.</summary>
    public NumericPortBuilder<T> Max(T? maxValue) {
        MaxValue = maxValue;
        return this;
    }
    /// <summary>Set both minimum and maximum allowed values.</summary>
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
