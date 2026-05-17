namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Base interface for all pipeline ports. Each port has a unique ID, a human-readable name,
/// and the <see cref="PortType"/> that values flowing through it must satisfy.
/// </summary>
public interface IPort {
    string Name { get; }
    Guid ID { get; }
    Type PortType { get; }
}
