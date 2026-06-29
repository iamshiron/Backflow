namespace Shiron.Lib.Pipeline.Generic;

public sealed class BlueprintPort : Port.IPort {
    public string Name { get; }
    public int ID { get; }
    public Type PortType { get; }
    internal int TypeParameterIndex { get; }
    internal PortDirection Direction { get; }

    internal BlueprintPort(string name, int channel, int typeParameterIndex, PortDirection direction) {
        Name = name;
        ID = channel;
        PortType = typeof(void);
        TypeParameterIndex = typeParameterIndex;
        Direction = direction;
    }
}
