using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Samples.Types;

namespace Shiron.Backflow.Samples.Nodes;

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

        UseCache = true;
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
