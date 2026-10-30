using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Validator;

namespace Shiron.Backflow.Samples.AspNetCore;

/// <summary>Adds two integers, demonstrating a real host-published node declaration.</summary>
public sealed class AddNode : AbstractNode {
    private readonly IInputPort<int> _left;
    private readonly IInputPort<int> _right;
    private readonly IOutputPort<int> _sum;

    /// <summary>Declare typed inputs and output.</summary>
    public AddNode() {
        _left = Input(new InputPort<int>("left", 0, new PassAllPortValidator<int>()));
        _right = Input(new InputPort<int>("right", 0, new PassAllPortValidator<int>()));
        _sum = Output(new OutputPort<int>("sum"));
    }

    /// <inheritdoc />
    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        _sum.Write(context, checked(_left.Read(context) + _right.Read(context)));
        return ValueTask.FromResult(true);
    }
}
