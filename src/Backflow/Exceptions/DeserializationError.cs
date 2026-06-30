namespace Shiron.Backflow.Exceptions;

/// <summary>
/// Represents a single error discovered while validating or deserializing a pipeline.
/// Multiple <see cref="DeserializationError"/>s are accumulated and surfaced together
/// via <see cref="PipelineDeserializationException"/>.
/// </summary>
/// <param name="Phase">The deserialization stage at which the error was detected.</param>
/// <param name="Message">Human-readable description of what went wrong.</param>
/// <param name="NodeId">The node instance ID involved, if applicable.</param>
/// <param name="PortName">The port name involved, if applicable.</param>
/// <param name="EdgeIndex">The zero-based index of the edge in the DTO, if applicable.</param>
public sealed record DeserializationError(
    DeserializationPhase Phase,
    string Message,
    string? NodeId = null,
    string? PortName = null,
    int? EdgeIndex = null
);
