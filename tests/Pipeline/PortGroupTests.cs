using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class ArrayInputPortTests {
    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;
        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class ArrayNode : AbstractNode {
        public readonly IArrayInputPort<int> Values;
        public readonly IOutputPort<int> Result;

        public ArrayNode() {
            Values = Input(
                new ArrayPortBuilder<int>(nameof(Values))
                    .Using(new NumericPortBuilder<int>(""))
                    .MinCount(1)
                    .MaxCount(5)
                    .Input()
            );
            Result = Output(new OutputPort<int>("result"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var values = Values.Read(context);
            Result.Write(context, values?.Sum() ?? 0);
            return ValueTask.FromResult(true);
        }
    }

    private class OptionalArrayNode : AbstractNode {
        public readonly IArrayInputPort<int> Values;

        public OptionalArrayNode() {
            Values = Input(
                new ArrayPortBuilder<int>(nameof(Values))
                    .Using(new NumericPortBuilder<int>(""))
                    .Input()
            );
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return ValueTask.FromResult(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void AddNode_WithArrayCounts_FreezesPortCount() {
        var builder = new PipelineBuilder(_registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 3 });

        Assert.Equal(3, node.Values.Count);
        Assert.True(node.Values.IsFrozen);
    }

    [Fact]
    public void AddNode_WithZeroArrayCount_SucceedsWhenMinIsZero() {
        var builder = new PipelineBuilder(_registry);
        var node = new OptionalArrayNode();
        var instance = builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 0 });

        Assert.Equal(0, node.Values.Count);
        Assert.True(node.Values.IsFrozen);
    }

    [Fact]
    public void AddNode_BelowMinCount_Throws() {
        var builder = new PipelineBuilder(_registry);
        var node = new ArrayNode();
        Assert.Throws<ArgumentException>(() =>
            builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 0 }));
    }

    [Fact]
    public void AddNode_AboveMaxCount_Throws() {
        var builder = new PipelineBuilder(_registry);
        var node = new ArrayNode();
        Assert.Throws<ArgumentException>(() =>
            builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 10 }));
    }

    [Fact]
    public void AddNode_WithoutArrayCounts_CountNotFrozen() {
        var builder = new PipelineBuilder(_registry);
        var node = new OptionalArrayNode();
        var instance = builder.AddNode(node);

        Assert.Null(node.Values.Count);
        Assert.False(node.Values.IsFrozen);
    }

    [Fact]
    public void AddConnection_ToArrayIndex_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var arrayNode = new ArrayNode();

        var srcInstance = builder.AddNode(source);
        var arrInstance = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 3 });

        var exception = Record.Exception(() =>
            builder.AddConnection(srcInstance, source.Out, arrInstance, (IPort) arrayNode.Values, 1));

        Assert.Null(exception);
    }

    [Fact]
    public void AddConnection_ToInvalidIndex_Throws() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var arrayNode = new ArrayNode();

        var srcInstance = builder.AddNode(source);
        var arrInstance = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 2 });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddConnection(srcInstance, source.Out, arrInstance, (IPort) arrayNode.Values, 5));
    }

    [Fact]
    public void AddConnection_ToNonArrayPortWithIndex_Throws() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var dest = new SourceNode();

        var srcInstance = builder.AddNode(source);
        var destInstance = builder.AddNode(dest);

        Assert.Throws<ArgumentException>(() =>
            builder.AddConnection(srcInstance, source.Out, destInstance, dest.Out, 0));
    }

    [Fact]
    public void Build_WithArrayConnections_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var s2 = new SourceNode();
        var s3 = new SourceNode();
        var arrayNode = new ArrayNode();

        var i1 = builder.AddNode(s1);
        var i2 = builder.AddNode(s2);
        var i3 = builder.AddNode(s3);
        var ai = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 3 });

        builder.AddConnection(i1, s1.Out, ai, (IPort) arrayNode.Values, 0);
        builder.AddConnection(i2, s2.Out, ai, (IPort) arrayNode.Values, 1);
        builder.AddConnection(i3, s3.Out, ai, (IPort) arrayNode.Values, 2);

        var pipeline = builder.Build();
        Assert.Equal(3, pipeline.Edges.Length);
        Assert.All(pipeline.Edges, e => Assert.True(e.SourceNode != null));
    }

    [Fact]
    public async Task Execution_AssembledArray_ReturnsAllValues() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var s2 = new SourceNode();
        var arrayNode = new ArrayNode();

        var i1 = builder.AddNode(s1);
        var i2 = builder.AddNode(s2);
        var ai = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 2 });

        builder.AddConnection(i1, s1.Out, ai, (IPort) arrayNode.Values, 0);
        builder.AddConnection(i2, s2.Out, ai, (IPort) arrayNode.Values, 1);

        var pipeline = builder.Build();
        var context = new PipelineContext();

        context.Write(i1, s1.Out, 10);
        context.Write(i2, s2.Out, 20);

        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        var result = context.Read<int>(ai, arrayNode.Result);
        Assert.Equal(30, result);
    }

    [Fact]
    public async Task Execution_DirectWrite_WorksForArrayPort() {
        var builder = new PipelineBuilder(_registry);
        var arrayNode = new ArrayNode();

        var ai = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 3 });

        var pipeline = builder.Build();
        var context = new PipelineContext();

        context.Write<int[]>(ai, (IPort) arrayNode.Values, [5, 10, 15]);

        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        Assert.Equal(30, context.Read<int>(ai, arrayNode.Result));
    }

    [Fact]
    public void EdgeInstance_CarriesDestIndex() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var arrayNode = new ArrayNode();

        var srcInstance = builder.AddNode(source);
        var arrInstance = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 2 });

        builder.AddConnection(srcInstance, source.Out, arrInstance, (IPort) arrayNode.Values, 1);

        var pipeline = builder.Build();
        var edge = pipeline.Edges[0];

        Assert.Equal(1, edge.DestIndex);
    }

    [Fact]
    public void EdgeInstance_RegularConnection_HasNullDestIndex() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var s2 = new SourceNode();

        var i1 = builder.AddNode(s1);
        var i2 = builder.AddNode(s2);

        builder.AddConnection(i1, s1.Out, i2, s2.Out);

        var pipeline = builder.Build();
        var edge = pipeline.Edges[0];

        Assert.Null(edge.DestIndex);
    }

    [Fact]
    public void ArrayInputPort_HasCorrectMetadata() {
        var node = new ArrayNode();
        var port = node.Values;

        Assert.Equal(typeof(int[]), port.PortType);
        Assert.Equal(1, port.MinCount);
        Assert.Equal(5, port.MaxCount);
        Assert.Equal("Values", port.Name);
    }

    [Fact]
    public async Task Execution_ArrayWithPartialData_UsesDefault() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var arrayNode = new ArrayNode();

        var i1 = builder.AddNode(s1);
        var ai = builder.AddNode(arrayNode, new Dictionary<string, int> { ["Values"] = 2 });

        builder.AddConnection(i1, s1.Out, ai, (IPort) arrayNode.Values, 0);

        var pipeline = builder.Build();
        var context = new PipelineContext();
        context.Write(i1, s1.Out, 42);

        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        Assert.Equal(42, context.Read<int>(ai, arrayNode.Result));
    }

    [Fact]
    public void SetCount_FreezesAfterFirstCall() {
        var node = new OptionalArrayNode();
        node.Values.SetCount(3);

        Assert.Equal(3, node.Values.Count);
        Assert.True(node.Values.IsFrozen);

        Assert.Throws<InvalidOperationException>(() => node.Values.SetCount(5));
    }
}
