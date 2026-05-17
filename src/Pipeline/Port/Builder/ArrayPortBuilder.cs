using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>
/// Fluent builder for array ports (<c>T[]</c>). Supports element count constraints and per-element
/// validation via <see cref="Using"/>.
/// </summary>
public class ArrayPortBuilder<T>(string name) : BasePortBuilder<ArrayPortBuilder<T>, T[]> {
    /// <summary>Minimum array length constraint.</summary>
    public int? MinLengthValue { get; protected set; }
    /// <summary>Maximum array length constraint.</summary>
    public int? MaxLengthValue { get; protected set; }
    /// <summary>Minimum number of indexed connections required.</summary>
    public int MinCountValue { get; private set; } = 0;
    /// <summary>Maximum number of indexed connections allowed, or <c>null</c> for unbounded.</summary>
    public int? MaxCountValue { get; private set; } = null;

    private IPortBuilderConfig<T>? _elementConfig;

    public ArrayPortBuilder<T> MinLength(int minLength) {
        MinLengthValue = minLength;
        return this;
    }
    public ArrayPortBuilder<T> MaxLength(int maxLength) {
        MaxLengthValue = maxLength;
        return this;
    }
    public ArrayPortBuilder<T> Range(int minLength, int maxLength) {
        return MinLength(minLength).MaxLength(maxLength);
    }

    /// <summary>Set the minimum element count (indexed connections).</summary>
    public ArrayPortBuilder<T> MinCount(int min) {
        MinCountValue = min;
        return this;
    }
    /// <summary>Set the maximum element count (indexed connections).</summary>
    public ArrayPortBuilder<T> MaxCount(int max) {
        MaxCountValue = max;
        return this;
    }

    /// <summary>Provide a builder config for per-element validation and defaults.</summary>
    public ArrayPortBuilder<T> Using(IPortBuilderConfig<T> elementConfig) {
        _elementConfig = elementConfig;
        return this;
    }

    public override IPortValidator<T[]> CreateValidator() {
        return new ArrayPortValidator<T>(this);
    }

    /// <summary>Build an <see cref="IArrayInputPort{T}"/> from this configuration.</summary>
    public new IArrayInputPort<T> Input() {
        if (!IsRequired && !IsNullable && !HasDefaultValue) {
            throw new InvalidOperationException("Non-nullable port requires a default value when it is optional.");
        }
        var port = CreateInput();
        if (port is Port portBase) portBase.IsRequired = IsRequired;
        return (IArrayInputPort<T>) port;
    }

    protected override IInputPort<T[]> CreateInput() {
        var elementValidator = _elementConfig?.CreateValidator() ?? new PassAllPortValidator<T>();
        var arrValidator = CreateValidator();

        if (_elementConfig is not null) {
            return new ArrayInputPort<T>(name, _elementConfig.DefaultValue, elementValidator, arrValidator, MinCountValue, MaxCountValue);
        }

        return new ArrayInputPort<T>(name, default, elementValidator, arrValidator, MinCountValue, MaxCountValue);
    }

    /// <summary>Build an <see cref="IArrayOutputPort{T}"/> from this configuration.</summary>
    public new IArrayOutputPort<T> Output() {
        return (IArrayOutputPort<T>) CreateOutput();
    }

    protected override IOutputPort<T[]> CreateOutput() {
        return new ArrayOutputPort<T>(name);
    }
}
