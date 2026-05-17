namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Cache key combining node type identity, assembly version, and input hash.
/// Two keys are equal when their <see cref="CombinedHash"/> matches.
/// </summary>
public interface ICacheKey {
    /// <summary>Fully-qualified node type name.</summary>
    string NodeType { get; }
    /// <summary>Version of the assembly containing the node type.</summary>
    string AssemblyVersion { get; }
    /// <summary>SHA-256 hash of the serialized input values.</summary>
    string InputHash { get; }
    /// <summary>SHA-256 hash of the combined <see cref="NodeType"/> + <see cref="AssemblyVersion"/> + <see cref="InputHash"/>.</summary>
    string CombinedHash { get; }
}
