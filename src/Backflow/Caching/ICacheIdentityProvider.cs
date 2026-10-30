namespace Shiron.Backflow.Caching;

/// <summary>Supplies deterministic semantic settings that affect cached execution results.</summary>
public interface ICacheIdentityProvider {
    /// <summary>Immutable JSON-serializable identity, including options, external data versions and context dependencies.</summary>
    object? CacheIdentity { get; }
}
