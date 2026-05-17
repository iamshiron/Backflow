namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>Extended builder config exposing validation and default-value metadata.</summary>
public interface IPortBuilderConfig<T> : IPortBuilder<T> {
    /// <summary>Create the validator for this port configuration.</summary>
    IPortValidator<T> CreateValidator();
    /// <summary>The configured default value.</summary>
    T? DefaultValue { get; }
    /// <summary>Whether the port is required (not optional).</summary>
    bool IsRequired { get; }
}
