using Shiron.Backflow.Exceptions;

namespace Shiron.Backflow.Serialization;

/// <summary>
/// Accumulates <see cref="DeserializationError"/>s during pipeline deserialization
/// and throws a single <see cref="PipelineDeserializationException"/> when asked.
/// </summary>
internal sealed class DeserializationErrorCollector {
    private readonly List<DeserializationError> _errors = [];

    /// <summary>Number of errors accumulated so far.</summary>
    public int Count => _errors.Count;

    /// <summary>Whether at least one error has been recorded.</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>Read-only view of all accumulated errors.</summary>
    public IReadOnlyList<DeserializationError> Errors => _errors;

    /// <summary>Record a new error with optional location context.</summary>
    public void Add(
        DeserializationPhase phase,
        string message,
        string? nodeId = null,
        string? portName = null,
        int? edgeIndex = null
    ) {
        _errors.Add(new DeserializationError(phase, message, nodeId, portName, edgeIndex));
    }

    /// <summary>
    /// Throw a <see cref="PipelineDeserializationException"/> wrapping every accumulated
    /// error, if any. Does nothing when no errors were recorded.
    /// </summary>
    public void ThrowIfErrors() {
        if (_errors.Count > 0)
            throw new PipelineDeserializationException(_errors);
    }
}
