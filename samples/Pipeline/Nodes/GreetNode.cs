using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Samples.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Nodes;

public class GreetNode : AbstractNode {
    public IInputPort<TimeOfDay> TimeOfDay { get; }
    public IOutputPort<string> Greeting { get; }

    public GreetNode() {
        TimeOfDay = Input(
            new EnumPortBuilder<TimeOfDay>(nameof(TimeOfDay))
                .Input()
        );
        Greeting = Output(
            new StringPortBuilder(nameof(Greeting))
                .Output()
        );
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        var time = TimeOfDay.Read(context);
        Greeting.Write(context, time switch {
            Types.TimeOfDay.Morning => "Good morning!",
            Types.TimeOfDay.Afternoon => "Good afternoon!",
            Types.TimeOfDay.Evening => "Good evening!",
            Types.TimeOfDay.Night => "Good night!",
            _ => "Hello!"
        });
        return ValueTask.FromResult(true);
    }
}
