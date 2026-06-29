using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Caching;

/// <summary>
/// Default <see cref="ICacheKeyFactory"/>. Serializes input port values to JSON, hashes them with SHA-256,
/// and combines with node type name and assembly version.
/// </summary>
public class CacheKeyFactory : ICacheKeyFactory {
    private readonly JsonSerializerOptions _jsonOptions;

    public CacheKeyFactory() {
        _jsonOptions = new JsonSerializerOptions();
    }

    public CacheKeyFactory(CacheTypeAdapterRegistry? typeAdapters) {
        _jsonOptions = new JsonSerializerOptions();
        typeAdapters?.ApplyTo(_jsonOptions);
    }

    public ICacheKey CreateKey(PipelineBuilder.NodeInstance node, IPipelineContext context) {
        var nodeType = node.Node.GetType();
        var typeName = ResolveTypeName(nodeType);
        var assemblyVersion = nodeType.Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        var inputHash = ComputeInputHash(node, context);

        return new CacheKey(typeName, assemblyVersion, inputHash);
    }

    private static string ResolveTypeName(Type type) {
        if (type.IsGenericType) {
            var genericDef = type.GetGenericTypeDefinition().FullName!;
            var args = string.Join(",", type.GetGenericArguments().Select(a => a.FullName ?? a.Name));
            return $"{genericDef}<{args}>";
        }
        return type.FullName ?? type.Name;
    }

    private string ComputeInputHash(PipelineBuilder.NodeInstance node, IPipelineContext context) {
        var sortedInputs = node.Node.Inputs
            .OrderBy(p => p.Name)
            .ToList();

        var parts = new List<string>();
        foreach (var port in sortedInputs) {
            var channel = node.Mappings[port];
            var value = context.ReadAny(channel);
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);
            parts.Add($"{port.Name}={serialized}");
        }

        var combined = string.Join(';', parts);
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
