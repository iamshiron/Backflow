using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>Host-controlled endpoint access, serialization and resource bounds.</summary>
public sealed class BackflowOptions {
    /// <summary>Authorization policy, or the default authenticated-user policy when null.</summary>
    public string? AuthorizationPolicy { get; set; }
    /// <summary>Explicitly allow anonymous access. Defaults to false.</summary>
    public bool AllowAnonymous { get; set; }
    /// <summary>Map execution submission only when a host scheduler is also registered.</summary>
    public bool EnableExecution { get; set; }
    /// <summary>Maximum body bytes, enforced even without Content-Length.</summary>
    public int MaximumRequestBytes { get; set; } = 1_048_576;
    /// <summary>Maximum nodes per graph.</summary>
    public int MaximumNodes { get; set; } = 256;
    /// <summary>Maximum edges per graph.</summary>
    public int MaximumEdges { get; set; } = 1024;
    /// <summary>Maximum literals per execution request.</summary>
    public int MaximumInputs { get; set; } = 1024;
    /// <summary>Maximum identifier length.</summary>
    public int MaximumIdentifierLength { get; set; } = 256;
    /// <summary>Maximum array input index, exclusive.</summary>
    public int MaximumArrayLength { get; set; } = 4096;
    /// <summary>Maximum JSON nesting depth.</summary>
    public int MaximumJsonDepth { get; set; } = 32;
    /// <summary>Serializer used for declared input types and derived catalog schemas. Frozen at registration.</summary>
    public JsonSerializerOptions ValueSerializerOptions { get; set; } = new(JsonSerializerDefaults.Web) {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    internal void Validate() {
        if (MaximumRequestBytes <= 0 || MaximumNodes <= 0 || MaximumEdges <= 0 || MaximumInputs <= 0 ||
            MaximumIdentifierLength <= 0 || MaximumArrayLength <= 0 || MaximumJsonDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(BackflowOptions), "All resource bounds must be positive.");
        ValueSerializerOptions = new JsonSerializerOptions(ValueSerializerOptions) { MaxDepth = MaximumJsonDepth };
        ValueSerializerOptions.MakeReadOnly(populateMissingResolver: true);
    }
}
