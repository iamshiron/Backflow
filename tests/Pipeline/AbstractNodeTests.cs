using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class AbstractNodeTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) {
            return null;
        }
    }

    private class NodeWithPorts : AbstractNode {
        public readonly IInputPort<int> InPort;
        public readonly IOutputPort<string> OutPort;

        public NodeWithPorts() {
            InPort = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            OutPort = Output(new OutputPort<string>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class EmptyNode : AbstractNode {
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class FakeInputPort : IInputPort<int> {
        public string Name => "fake";
        public Guid ID => Guid.NewGuid();
        public int Read(INodeContext context) {
            return 0;
        }
        public object? ReadAny(INodeContext context) {
            return null;
        }
        public bool TryRead(INodeContext context, out int value) {
            value = 0;
            return true;
        }
        public bool HasValue(INodeContext context) {
            return false;
        }
    }

    private class FakeOutputPort : IOutputPort<int> {
        public string Name => "fake";
        public Guid ID => Guid.NewGuid();
        public void Write(INodeContext context, int value) { }
    }

    [Fact]
    public void Input_RegistersPortInInputsList() {
        var node = new NodeWithPorts();
        Assert.Single(node.Inputs);
        Assert.Equal("in", node.Inputs[0].Name);
    }

    [Fact]
    public void Output_RegistersPortInOutputsList() {
        var node = new NodeWithPorts();
        Assert.Single(node.Outputs);
        Assert.Equal("out", node.Outputs[0].Name);
    }

    [Fact]
    public void Ports_ReturnsAllPorts() {
        var node = new NodeWithPorts();
        var ports = node.Ports;
        Assert.Equal(2, ports.Count);
        Assert.Contains(ports, p => p.Name == "in");
        Assert.Contains(ports, p => p.Name == "out");
    }

    [Fact]
    public void EmptyNode_NoPorts() {
        var node = new EmptyNode();
        Assert.Empty(node.Inputs);
        Assert.Empty(node.Outputs);
        Assert.Empty(node.Ports);
    }

    [Fact]
    public void Input_InvalidPortType_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => {
            new BadInputNode();
        });
    }

    [Fact]
    public void Output_InvalidPortType_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => {
            new BadOutputNode();
        });
    }

    private class BadInputNode : AbstractNode {
        public BadInputNode() {
            Input(new FakeInputPort());
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class BadOutputNode : AbstractNode {
        public BadOutputNode() {
            Output(new FakeOutputPort());
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

}
