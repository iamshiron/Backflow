using System.Text;

namespace Shiron.Backflow.Exceptions;

/// <summary>
/// Thrown when a pipeline definition or inputs JSON cannot be deserialized.
/// Collects every recoverable error discovered during validation into a single
/// aggregate so callers see all problems at once rather than one-at-a-time.
/// </summary>
public sealed class PipelineDeserializationException : Exception {
    /// <summary>
    /// Every individual error discovered during deserialization, in detection order.
    /// </summary>
    public IReadOnlyList<DeserializationError> Errors { get; }

    /// <summary>Create an exception wrapping a single error.</summary>
    public PipelineDeserializationException(DeserializationError error, Exception? innerException = null)
        : base(BuildMessage([error]), innerException) {
        Errors = [error];
    }

    /// <summary>Create an exception wrapping multiple accumulated errors.</summary>
    public PipelineDeserializationException(IReadOnlyList<DeserializationError> errors, Exception? innerException = null)
        : base(BuildMessage(errors), innerException) {
        Errors = errors;
    }

    private static string BuildMessage(IReadOnlyList<DeserializationError> errors) {
        if (errors.Count == 0)
            return "Pipeline deserialization failed.";

        if (errors.Count == 1) {
            var e = errors[0];
            return $"Pipeline deserialization failed: {e.Message}{BuildContextString(e)}";
        }

        var sb = new StringBuilder();
        sb.Append($"Pipeline deserialization failed with {errors.Count} error(s):");
        for (var i = 0; i < errors.Count; i++) {
            sb.Append($"\n  [{i + 1}] [{errors[i].Phase}] {errors[i].Message}{BuildContextString(errors[i])}");
        }
        return sb.ToString();
    }

    private static string BuildContextString(DeserializationError e) {
        var parts = new List<string>(3);
        if (e.NodeId is not null) parts.Add($"node: '{e.NodeId}'");
        if (e.PortName is not null) parts.Add($"port: '{e.PortName}'");
        if (e.EdgeIndex is not null) parts.Add($"edge: #{e.EdgeIndex}");
        return parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
    }
}
