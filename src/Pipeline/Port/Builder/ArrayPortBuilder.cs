using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class ArrayPortBuilder<T>(string name) : BasePortBuilder<ArrayPortBuilder<T>, T[]> {
    public int? MinLengthValue { get; protected set; }
    public int? MaxLengthValue { get; protected set; }
    public int MinCountValue { get; private set; } = 0;
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

    public ArrayPortBuilder<T> MinCount(int min) {
        MinCountValue = min;
        return this;
    }
    public ArrayPortBuilder<T> MaxCount(int max) {
        MaxCountValue = max;
        return this;
    }

    public ArrayPortBuilder<T> Using(IPortBuilderConfig<T> elementConfig) {
        _elementConfig = elementConfig;
        return this;
    }

    public override IPortValidator<T[]> CreateValidator() {
        return new ArrayPortValidator<T>(this);
    }

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

    protected override IOutputPort<T[]> CreateOutput() {
        return new OutputPort<T[]>(name);
    }
}
