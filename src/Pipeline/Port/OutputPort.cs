using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Concrete <see cref="IOutputPort{T}"/> implementation. Writes values directly into the execution context.
/// </summary>
public class OutputPort<T>(string name) : Port(name), IOutputPort<T> {
    public override Type PortType { get; protected set; } = typeof(T);
    public void Write(INodeContext context, T? value) {
        context.Write(this, value);
    }
}
