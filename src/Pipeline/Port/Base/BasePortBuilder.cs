using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Base;

public abstract class BasePortBuilder<TBuilder, TValue> : IPortBuilderConfig<TValue> where TBuilder : BasePortBuilder<TBuilder, TValue> {
    public bool IsRequired { get; private set; } = true;
    public bool IsNullable { get; private set; } = false;
    public TValue? DefaultValue { get; private set; } = default;
    public bool HasDefaultValue { get; private set; } = false;

    public TBuilder Optional(bool optional = true) {
        IsRequired = !optional;
        return (TBuilder) this;
    }
    public TBuilder Nullable(bool nullable = true) {
        IsNullable = nullable;
        return (TBuilder) this;
    }
    public TBuilder Default(TValue? value) {
        DefaultValue = value;
        HasDefaultValue = value is not null;
        return (TBuilder) this;
    }
    public IInputPort<TValue> Input() {
        if (!IsRequired && !IsNullable && !HasDefaultValue) {
            throw new InvalidOperationException("Non-nullable port requires a default value when it is optional.");
        }
        var port = CreateInput();
        if (port is Port portBase) portBase.IsRequired = IsRequired;
        return port;
    }
    public IOutputPort<TValue> Output() {
        return CreateOutput();
    }
    protected abstract IInputPort<TValue> CreateInput();
    protected abstract IOutputPort<TValue> CreateOutput();

    public abstract IPortValidator<TValue> CreateValidator();
}
