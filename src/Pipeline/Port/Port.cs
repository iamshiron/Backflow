namespace Shiron.Lib.Pipeline.Port;

public class Port(string name) : IPort {
    public string Name { get; } = name;
    public Guid ID { get; } = Guid.NewGuid();
    public bool IsRequired { get; set; } = true;
    public virtual Type PortType { get; protected set; } = typeof(void);
}
