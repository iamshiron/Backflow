namespace Shiron.Lib.Pipeline.Generic;

public sealed class BlueprintPort : Port.IPort {
    public string Name { get; }
    public Guid ID { get; }
    public Type PortType { get; }
    internal int TypeParameterIndex { get; }
    internal PortDirection Direction { get; }

    internal BlueprintPort(string name, Guid id, int typeParameterIndex, PortDirection direction) {
        Name = name;
        ID = id;
        PortType = typeof(void);
        TypeParameterIndex = typeParameterIndex;
        Direction = direction;
    }
}
