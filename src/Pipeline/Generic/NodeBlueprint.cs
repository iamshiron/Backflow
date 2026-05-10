namespace Shiron.Lib.Pipeline.Generic;

public sealed class NodeBlueprint {
    public Type OpenType { get; }
    public string DisplayName { get; }
    public TypeParameterInfo[] TypeParameters { get; }
    public PortTypeMetadata[] Ports { get; }

    public NodeBlueprint(Type openType, string displayName, TypeParameterInfo[] typeParameters, PortTypeMetadata[] ports) {
        OpenType = openType;
        DisplayName = displayName;
        TypeParameters = typeParameters;
        Ports = ports;
    }

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
