namespace Shiron.Lib.Pipeline.Exceptions;

/// <summary>Thrown when a required node type is not registered in the <see cref="Registry.NodeRegistry"/>.</summary>
public class NodeNotRegisteredException(
    string nodeTypeName,
    string? nodeId = null,
    bool isGeneric = false
) : Exception(
    isGeneric
        ? $"Generic node blueprint '{nodeTypeName}' is not registered in the node registry."
            + (nodeId is not null ? $" Required by node instance '{nodeId}'." : "")
        : $"Node type '{nodeTypeName}' is not registered in the node registry."
            + (nodeId is not null ? $" Required by node instance '{nodeId}'." : "")
) {
    /// <summary>The full type name that was not found.</summary>
    public string NodeTypeName { get; } = nodeTypeName;

    /// <summary>The instance ID that required the node, if known.</summary>
    public string? NodeId { get; } = nodeId;

    /// <summary>Whether the missing registration was for a generic node blueprint.</summary>
    public bool IsGeneric { get; } = isGeneric;
}
