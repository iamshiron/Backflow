using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// A named data endpoint on a node. Connected ports share a GUID-mapped slot in the pipeline context.
/// </summary>
public class Port(string name) {
    /// <summary>Unique identifier for this port's data channel.</summary>
    public Guid ID { get; private set; } = Guid.NewGuid();

    /// <summary>Human-readable port name.</summary>
    public string Name { get; } = name;

    /// <summary>Write a value to this port's data channel.</summary>
    /// <param name="context">Node context to write into.</param>
    /// <param name="value">Value to write.</param>
    public void Write(INodeContext context, object value) {
        context.Write(this, value);
    }

    /// <summary>Read a value from this port's data channel.</summary>
    /// <param name="context">Node context to read from.</param>
    public T? Read<T>(INodeContext context) where T : new() {
        var value = context.Read(this);
        return (T?) value;
    }

    public override string ToString() {
        return $"Port: {Name} (ID: {ID})";
    }
}
