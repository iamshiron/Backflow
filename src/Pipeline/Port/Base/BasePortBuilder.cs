using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Base;

public abstract class BasePortBuilder<TBuilder, TValue> : IPortBuilder<TValue> where TBuilder : BasePortBuilder<TBuilder, TValue> {
    public bool IsRequired { get; private set; } = true;
    public bool IsNullable { get; private set; } = false;
    public TValue? DefaultValue { get; private set; } = default;

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
        return (TBuilder) this;
    }

    public IInputPort<TValue> Input() {
        if (!IsRequired && !IsNullable && DefaultValue is null) {
            throw new InvalidOperationException("Non-nullable port requires a default value when it is optional.");
        }
        return CreateInput();
    }
    public IOutputPort<TValue> Output() {
        return CreateOutput();
    }

    protected abstract IInputPort<TValue> CreateInput();
    protected abstract IOutputPort<TValue> CreateOutput();
}
