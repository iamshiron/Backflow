namespace Shiron.Backflow.Port;

/// <summary>
/// Base class for all port implementations. Carries an <see cref="ID"/>, a <see cref="Name"/>,
/// and an <see cref="IsRequired"/> flag that controls propagation-skipping during execution.
/// </summary>
public class Port(string name) : IPort {
    private bool _frozen;
    private bool _isRequired = true;
    internal void Freeze() => _frozen = true;
    public string Name { get; } = name;
    public int ID { get; internal set; }
    /// <summary>When <c>true</c>, downstream nodes are skipped if this port receives no value.</summary>
    public bool IsRequired {
        get => _isRequired;
        set {
            if (_frozen) throw new InvalidOperationException("Registered port definitions are immutable.");
            _isRequired = value;
        }
    }
    public virtual Type PortType { get; protected set; } = typeof(void);
}
