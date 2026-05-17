using System.Security.Cryptography;
using System.Text;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Default <see cref="ICacheKey"/> implementation. Computes a <see cref="CombinedHash"/> via SHA-256
/// over the concatenation of <see cref="NodeType"/>, <see cref="AssemblyVersion"/>, and <see cref="InputHash"/>.
/// </summary>
public sealed class CacheKey : ICacheKey {
    public string NodeType { get; }
    public string AssemblyVersion { get; }
    public string InputHash { get; }
    public string CombinedHash { get; }

    public CacheKey(string nodeType, string assemblyVersion, string inputHash) {
        NodeType = nodeType;
        AssemblyVersion = assemblyVersion;
        InputHash = inputHash;
        CombinedHash = ComputeCombinedHash(nodeType, assemblyVersion, inputHash);
    }

    private static string ComputeCombinedHash(string nodeType, string assemblyVersion, string inputHash) {
        var combined = string.Join('|', nodeType, assemblyVersion, inputHash);
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public bool Equals(ICacheKey? other) {
        return other is not null && CombinedHash == other.CombinedHash;
    }

    public override bool Equals(object? obj) {
        return obj is ICacheKey other && Equals(other);
    }

    public override int GetHashCode() {
        return CombinedHash.GetHashCode();
    }

    public override string ToString() {
        return CombinedHash;
    }
}
