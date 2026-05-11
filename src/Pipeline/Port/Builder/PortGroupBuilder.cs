namespace Shiron.Lib.Pipeline.Port.Builder;

public class PortGroupBuilder<T>(string name) {
    private IPortBuilderConfig<T>? _elementConfig;

    public int MinCountValue { get; private set; } = 0;
    public int? MaxCountValue { get; private set; } = null;

    public PortGroupBuilder<T> Using(IPortBuilderConfig<T> elementConfig) {
        _elementConfig = elementConfig;
        return this;
    }

    public PortGroupBuilder<T> MinCount(int min) {
        MinCountValue = min;
        return this;
    }

    public PortGroupBuilder<T> MaxCount(int max) {
        MaxCountValue = max;
        return this;
    }

    public InputPortGroup<T> Input() {
        if (_elementConfig is null) {
            throw new InvalidOperationException(
                "Element port builder not configured. Call Using() first.");
        }

        var validator = _elementConfig.CreateValidator();
        return new InputPortGroup<T>(
            name, _elementConfig.DefaultValue, validator,
            MinCountValue, MaxCountValue
        );
    }
}
