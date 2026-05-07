using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Uniquely and deterministically identifies a node execution across any pipeline.
/// Composed of the node's full type name, its assembly version, and a SHA-256 hash
/// of all input port values.
/// </summary>
public sealed class CacheKey : IEquatable<CacheKey> {
    public string NodeType { get; }
    public string AssemblyVersion { get; }
    public string InputHash { get; }

    internal CacheKey(string nodeType, string assemblyVersion, string inputHash) {
        NodeType = nodeType;
        AssemblyVersion = assemblyVersion;
        InputHash = inputHash;
    }

    /// <summary>
    /// Builds a <see cref="CacheKey"/> from a node and its resolved input values.
    /// </summary>
    /// <param name="node">The node whose type and assembly identity form part of the key.</param>
    /// <param name="inputs">
    /// Ordered enumerable of <c>(PortName, Type, Value)</c> tuples — one per input port.
    /// Ordering must be deterministic (e.g., by port name) for the hash to be stable.
    /// </param>
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
                var json = JsonSerializer.Serialize(value, portType);
                sb.Append(json);
            }
        }

        return sb.ToString();
    }

    private static string Sha256Hex(string input) {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Creates a composite string key suitable for dictionary / JSON look-ups.
    /// </summary>
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
