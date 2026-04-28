using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline;

public class Port(string name) {
    public Guid ID { get; private set; } = Guid.NewGuid();
    public string Name { get; } = name;

    public void Write(INodeContext context, object value) {
        context.Write(this, value);
    }

    public T? Read<T>(INodeContext context) where T : new() {
        var value = context.Read(this);
        return (T?) value;
    }

    public override string ToString() {
        return $"Port: {Name} (ID: {ID})";
    }
}
