using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline.Caching;

public sealed class CacheKey : IEquatable<CacheKey> {
    public string NodeType { get; }
    public string AssemblyVersion { get; }
    public string InputHash { get; }

    internal CacheKey(string nodeType, string assemblyVersion, string inputHash) {
        NodeType = nodeType;
        AssemblyVersion = assemblyVersion;
        InputHash = inputHash;
    }

    public static CacheKey Create(AbstractNode node, IEnumerable<(string PortName, Type Type, object? Value)> inputs) {
        var type = node.GetType();
        var nodeType = type.FullName!;
        var assemblyVersion = type.Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

        var payload = BuildPayload(nodeType, assemblyVersion, inputs);
        var hash = Sha256Hex(payload);
        return new CacheKey(nodeType, assemblyVersion, hash);
    }

    private static string BuildPayload(
        string nodeType,
        string assemblyVersion,
        IEnumerable<(string PortName, Type Type, object? Value)> inputs
    ) {
        var sb = new StringBuilder();
        sb.Append(nodeType);
        sb.Append('|');
        sb.Append(assemblyVersion);

        foreach (var (portName, portType, value) in inputs) {
            sb.Append('|');
            sb.Append(portName);
            sb.Append(':');
            sb.Append(portType.FullName);
            sb.Append('=');

            if (value is not null) {
                if (value is ICacheHashable hashable) {
                    sb.Append("hash:");
                    sb.Append(hashable.GetCacheHash());
                } else {
                    var json = JsonSerializer.Serialize(value, portType);
                    sb.Append(json);
                }
            }
        }

        return sb.ToString();
    }

    private static string Sha256Hex(string input) {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public string ToCompositeKey() {
        return $"{NodeType}:{AssemblyVersion}:{InputHash}";
    }

    public bool Equals(CacheKey? other) {
        if (other is null) return false;
        return NodeType == other.NodeType
            && AssemblyVersion == other.AssemblyVersion
            && InputHash == other.InputHash;
    }

    public override bool Equals(object? obj) {
        return Equals(obj as CacheKey);
    }
    public override int GetHashCode() {
        return HashCode.Combine(NodeType, AssemblyVersion, InputHash);
    }
    public override string ToString() {
        return ToCompositeKey();
    }
}
