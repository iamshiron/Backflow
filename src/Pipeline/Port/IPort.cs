namespace Shiron.Lib.Pipeline.Port;

public interface IPort {
    string Name { get; }
    Guid ID { get; }
    Type PortType { get; }
}
