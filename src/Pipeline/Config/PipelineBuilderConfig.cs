namespace Shiron.Lib.Pipeline.Config;

public record PipelineBuilderConfig {
    public bool StrictTypeCasting { get; init; }
    public bool EnableCaching { get; init; }
}
