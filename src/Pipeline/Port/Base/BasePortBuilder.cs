using Shiron.Lib.Pipeline.Port.Builder;

namespace Shiron.Lib.Pipeline.Port.Base;

/// <summary>
/// Abstract base for port builders. Provides the fluent API for <see cref="Optional"/>,
/// <see cref="Nullable"/>, <see cref="Default"/>, <see cref="Input"/>, and <see cref="Output"/>.
/// </summary>
public abstract class BasePortBuilder<TBuilder, TValue> : IPortBuilderConfig<TValue> where TBuilder : BasePortBuilder<TBuilder, TValue> {
    public bool IsRequired { get; private set; } = true;
    public bool IsNullable { get; private set; } = false;
    public TValue? DefaultValue { get; private set; } = default;
    public bool HasDefaultValue { get; private set; } = false;

    /// <summary>Mark the port as optional (non-required).</summary>
    public TBuilder Optional(bool optional = true) {
        IsRequired = !optional;
        return (TBuilder) this;
    }
    /// <summary>Allow <c>null</c> values through the port.</summary>
    public TBuilder Nullable(bool nullable = true) {
        IsNullable = nullable;
        return (TBuilder) this;
    }
    /// <summary>Set the fallback value when the port is unread.</summary>
    public TBuilder Default(TValue? value) {
        DefaultValue = value;
        HasDefaultValue = value is not null;
        return (TBuilder) this;
    }
    /// <summary>Build an input port from this configuration.</summary>
    public IInputPort<TValue> Input() {
        if (!IsRequired && !IsNullable && !HasDefaultValue) {
            throw new InvalidOperationException("Non-nullable port requires a default value when it is optional.");
        }
        var port = CreateInput();
        if (port is Port portBase) portBase.IsRequired = IsRequired;
        return port;
    }
    /// <summary>Build an output port from this configuration.</summary>
    public IOutputPort<TValue> Output() {
        return CreateOutput();
    }
    protected abstract IInputPort<TValue> CreateInput();
    protected abstract IOutputPort<TValue> CreateOutput();

    public abstract IPortValidator<TValue> CreateValidator();
}
