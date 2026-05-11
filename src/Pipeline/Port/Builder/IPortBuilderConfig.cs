namespace Shiron.Lib.Pipeline.Port.Builder;

public interface IPortBuilderConfig<T> : IPortBuilder<T> {
    IPortValidator<T> CreateValidator();
    T? DefaultValue { get; }
    bool IsRequired { get; }
}
