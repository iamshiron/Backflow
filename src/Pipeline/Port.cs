using Shiron.Lib.Pipeline.Context;

namespace Shiron.Lib.Pipeline;

public class Port {
    public Guid ID { get; private set; } = Guid.NewGuid();

    public void Write(INodeContext context, object value) {
        context.Write(this, value);
    }

    public T? Read<T>(INodeContext context) where T : new() {
        var value = context.Read(this);
        return (T?) value;
    }
}
