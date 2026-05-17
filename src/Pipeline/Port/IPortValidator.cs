namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Validates a port value at runtime. Returns <c>null</c> if valid, or an error description string if invalid.
/// Used for fail-fast semantics — validation runs on every read.
/// </summary>
public interface IPortValidator<in T> {
    /// <summary>Validate the given value. Returns <c>null</c> if valid.</summary>
    string? Validate(T? value);
}
