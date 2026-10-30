using System.Security.Cryptography;
using System.Text;

namespace Shiron.Backflow.Caching;

/// <summary>
/// Default <see cref="ICacheKey"/> implementation. Computes a <see cref="CombinedHash"/> via SHA-256
/// over a versioned, length-delimited encoding of type, assembly version, and input hash.
/// </summary>
public sealed class CacheKey : ICacheKey {
    /// <inheritdoc/>
    public string NodeType { get; }
    /// <inheritdoc/>
    public string AssemblyVersion { get; }
    /// <inheritdoc/>
    public string InputHash { get; }
    /// <inheritdoc/>
    public string CombinedHash { get; }

    /// <summary>Creates an unambiguous versioned cache identity.</summary>
    public CacheKey(string nodeType, string assemblyVersion, string inputHash) {
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(assemblyVersion);
        ArgumentNullException.ThrowIfNull(inputHash);
        NodeType = nodeType;
        AssemblyVersion = assemblyVersion;
        InputHash = inputHash;
        CombinedHash = ComputeCombinedHash(nodeType, assemblyVersion, inputHash);
    }

    private static string ComputeCombinedHash(string nodeType, string assemblyVersion, string inputHash) {
        var combined = System.Text.Json.JsonSerializer.Serialize(new[] { "backflow-cache-v2", nodeType, assemblyVersion, inputHash });
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Compares combined cache identity hashes.</summary>
    public bool Equals(ICacheKey? other) {
        return other is not null && CombinedHash == other.CombinedHash;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is ICacheKey other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return CombinedHash.GetHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() {
        return CombinedHash;
    }
}
