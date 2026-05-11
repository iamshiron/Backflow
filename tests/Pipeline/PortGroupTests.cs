using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PortGroupTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;
        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class GroupNode : AbstractNode {
        public readonly IInputPortGroup<int> Values;
        public readonly IOutputPort<int> Result;

        public GroupNode() {
            Values = InputGroup(
                new PortGroupBuilder<int>(nameof(Values))
                    .Using(new NumericPortBuilder<int>(""))
                    .MinCount(1)
                    .MaxCount(5)
                    .Input()
            );
            Result = Output(new OutputPort<int>("result"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var values = Values.ReadAll(context);
            Result.Write(context, values.Sum());
            return ValueTask.FromResult(true);
        }
    }

    private class OptionalGroupNode : AbstractNode {
        public readonly IInputPortGroup<int> Values;

        public OptionalGroupNode() {
            Values = InputGroup(
                new PortGroupBuilder<int>(nameof(Values))
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
    public void AddNode_WithGroupCounts_CreatesGroupMappings() {
        var builder = new PipelineBuilder(_registry);
        var node = new GroupNode();
        var instance = builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 3 });

        Assert.True(instance.GroupMappings.ContainsKey(node.Values));
        Assert.Equal(3, instance.GroupMappings[node.Values].Count);
        Assert.Equal(3, instance.GroupMappings[node.Values].Keys.Max() + 1);
    }

    [Fact]
    public void AddNode_WithZeroGroupCount_SucceedsWhenMinIsZero() {
        var builder = new PipelineBuilder(_registry);
        var node = new OptionalGroupNode();
        var instance = builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 0 });

        Assert.True(instance.GroupMappings.ContainsKey(node.Values));
        Assert.Empty(instance.GroupMappings[node.Values]);
    }

    [Fact]
    public void AddNode_BelowMinCount_Throws() {
        var builder = new PipelineBuilder(_registry);
        var node = new GroupNode();
        Assert.Throws<ArgumentException>(() =>
            builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 0 }));
    }

    [Fact]
    public void AddNode_AboveMaxCount_Throws() {
        var builder = new PipelineBuilder(_registry);
        var node = new GroupNode();
        Assert.Throws<ArgumentException>(() =>
            builder.AddNode(node, new Dictionary<string, int> { ["Values"] = 10 }));
    }

    [Fact]
    public void AddNode_WithoutGroupCounts_DefaultsToZero() {
        var builder = new PipelineBuilder(_registry);
        var node = new OptionalGroupNode();
        var instance = builder.AddNode(node);

        Assert.True(instance.GroupMappings.ContainsKey(node.Values));
        Assert.Empty(instance.GroupMappings[node.Values]);
    }

    [Fact]
    public void AddConnection_ToGroupIndex_SharesChannelMapping() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var groupNode = new GroupNode();

        var srcInstance = builder.AddNode(source);
        var groupInstance = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 3 });

        builder.AddConnection(srcInstance, source.Out, groupInstance, groupNode.Values, 1);

        Assert.Equal(srcInstance.Mappings[source.Out], groupInstance.GroupMappings[groupNode.Values][1]);
    }

    [Fact]
    public void AddConnection_ToInvalidIndex_Throws() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var groupNode = new GroupNode();

        var srcInstance = builder.AddNode(source);
        var groupInstance = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 2 });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddConnection(srcInstance, source.Out, groupInstance, groupNode.Values, 5));
    }

    [Fact]
    public void AddConnection_ToNonGroupPortWithIndex_Throws() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var dest = new SourceNode();

        var srcInstance = builder.AddNode(source);
        var destInstance = builder.AddNode(dest);

        Assert.Throws<ArgumentException>(() =>
            builder.AddConnection(srcInstance, source.Out, destInstance, dest.Out, 0));
    }

    [Fact]
    public void AddConnection_GroupIndexOutOfRange_Throws() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var groupNode = new OptionalGroupNode();

        var srcInstance = builder.AddNode(source);
        var groupInstance = builder.AddNode(groupNode);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddConnection(srcInstance, source.Out, groupInstance, groupNode.Values, 0));
    }

    [Fact]
    public void Build_WithGroupConnections_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var s2 = new SourceNode();
        var s3 = new SourceNode();
        var groupNode = new GroupNode();

        var i1 = builder.AddNode(s1);
        var i2 = builder.AddNode(s2);
        var i3 = builder.AddNode(s3);
        var gi = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 3 });

        builder.AddConnection(i1, s1.Out, gi, groupNode.Values, 0);
        builder.AddConnection(i2, s2.Out, gi, groupNode.Values, 1);
        builder.AddConnection(i3, s3.Out, gi, groupNode.Values, 2);

        var pipeline = builder.Build();
        Assert.Equal(3, pipeline.Edges.Length);
        Assert.All(pipeline.Edges, e => Assert.True(e.SourceNode != null));
    }

    [Fact]
    public async Task Execution_ReadAll_ReturnsAllValues() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var s2 = new SourceNode();
        var groupNode = new GroupNode();

        var i1 = builder.AddNode(s1);
        var i2 = builder.AddNode(s2);
        var gi = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 2 });

        builder.AddConnection(i1, s1.Out, gi, groupNode.Values, 0);
        builder.AddConnection(i2, s2.Out, gi, groupNode.Values, 1);

        var pipeline = builder.Build();
        var context = new PipelineContext();

        context.Write(i1, s1.Out, 10);
        context.Write(i2, s2.Out, 20);

        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        var result = context.Read<int>(gi, groupNode.Result);
        Assert.Equal(30, result);
    }

    [Fact]
    public async Task Execution_PipelineContext_WriteGroup_WritesToCorrectSlot() {
        var builder = new PipelineBuilder(_registry);
        var groupNode = new GroupNode();

        var gi = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 3 });

        var pipeline = builder.Build();
        var context = new PipelineContext();

        context.WriteGroup(gi, groupNode.Values, 0, 5);
        context.WriteGroup(gi, groupNode.Values, 1, 10);
        context.WriteGroup(gi, groupNode.Values, 2, 15);

        Assert.Equal(5, context.ReadGroup<int>(gi, groupNode.Values, 0));
        Assert.Equal(10, context.ReadGroup<int>(gi, groupNode.Values, 1));
        Assert.Equal(15, context.ReadGroup<int>(gi, groupNode.Values, 2));

        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        Assert.Equal(30, context.Read<int>(gi, groupNode.Result));
    }

    [Fact]
    public void EdgeInstance_CarriesDestIndex() {
        var builder = new PipelineBuilder(_registry);
        var source = new SourceNode();
        var groupNode = new GroupNode();

        var srcInstance = builder.AddNode(source);
        var groupInstance = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 2 });

        builder.AddConnection(srcInstance, source.Out, groupInstance, groupNode.Values, 1);

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
    public void InputPortGroup_HasCorrectMetadata() {
        var node = new GroupNode();
        var group = node.Values;

        Assert.Equal(typeof(int), group.PortType);
        Assert.Equal(typeof(int), group.ElementType);
        Assert.Equal(1, group.MinCount);
        Assert.Equal(5, group.MaxCount);
        Assert.Equal("Values", group.Name);
    }

    [Fact]
    public async Task Execution_GroupWithPartialData_UsesDefault() {
        var builder = new PipelineBuilder(_registry);
        var s1 = new SourceNode();
        var groupNode = new GroupNode();

        var i1 = builder.AddNode(s1);
        var gi = builder.AddNode(groupNode, new Dictionary<string, int> { ["Values"] = 2 });

        builder.AddConnection(i1, s1.Out, gi, groupNode.Values, 0);
        // Index 1 has no connection — should use default (0)

        var pipeline = builder.Build();
        var context = new PipelineContext();
        context.Write(i1, s1.Out, 42);

        var executor = new PipelineExecutor(pipeline);
        await executor.ExecuteAsync(context);

        Assert.Equal(42, context.Read<int>(gi, groupNode.Result));
    }
}
