namespace Shiron.Lib.Pipeline.Generic;

/// <summary>
/// Blueprint for an open-generic node type. Captures the open type, its type parameters,
/// and the port metadata needed for type inference during pipeline construction.
/// </summary>
public sealed class NodeBlueprint {
    /// <summary>The open generic type (e.g., <c>typeof(GenericAddNode&lt;&gt;)</c>).</summary>
    public Type OpenType { get; }
    /// <summary>Human-readable name without the backtick suffix.</summary>
    public string DisplayName { get; }
    /// <summary>Type parameter descriptors in declaration order.</summary>
    public TypeParameterInfo[] TypeParameters { get; }
    /// <summary>Port descriptors extracted from the open type's public properties.</summary>
    public PortTypeMetadata[] Ports { get; }

    public NodeBlueprint(Type openType, string displayName, TypeParameterInfo[] typeParameters, PortTypeMetadata[] ports) {
        OpenType = openType;
        DisplayName = displayName;
        TypeParameters = typeParameters;
        Ports = ports;
    }

    /// <summary>Look up a port descriptor by name.</summary>
    public PortTypeMetadata? GetPort(string name) {
        foreach (var port in Ports) {
            if (port.Name == name) return port;
        }
        return null;
    }

    public string MakeTypeName(Type[] args) {
        return OpenType.FullName!;
    }
}
