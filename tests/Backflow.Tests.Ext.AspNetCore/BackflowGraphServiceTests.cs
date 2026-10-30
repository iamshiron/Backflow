using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shiron.Backflow.Context;
using Shiron.Backflow.Ext.AspNetCore;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Validator;
using Shiron.Backflow.Registry;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.AspNetCore;

public sealed class BackflowGraphServiceTests {
    [Fact]
    public void TryCompile_DeclarationOnlyCatalog_RequiresNoNodeImplementationAndCannotExecute() {
        using var services = new ServiceCollection().AddBackflow((_, catalog) => {
            catalog.AddContract<SourceConfiguration>("source", ports => ports.Output<int>("value"));
            catalog.AddContract<SinkConfiguration>("sink", ports => ports.Input<int>("value"));
        }).BuildServiceProvider();
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var graph = new BackflowGraph([new("source-instance", "source"), new("sink-instance", "sink")],
            [new("source-instance", "value", "sink-instance", "value")]);
        Assert.True(graphs.TryCompile(graph, out var compiled, out _));
        Assert.Same(graph, compiled!.Graph);
        Assert.Equal(["source-instance", "sink-instance"], compiled.TopologicalNodeIds);
        Assert.False(graphs.TryBuild(graph, out _, out var result));
        Assert.Equal(BackflowErrorCode.NotExecutable, Assert.Single(result.Errors).Code);
        Assert.False(graphs.TryPrepareExecution(new(graph, []), out _, out _));
        Assert.All(graphs.GetCatalog().Nodes, node => {
            Assert.False(node.IsExecutable);
            Assert.NotNull(node.ConfigurationSchema);
        });
        Assert.False(graphs.Validate(graph with { Edges = [new("sink-instance", "value", "source-instance", "value")] }).IsValid);
        Assert.Empty(services.GetRequiredService<NodeRegistry>().GetAllBlueprints());
    }

    [Fact]
    public void TryCompile_ClosedGenericPublishedNode_DoesNotResolveClientTypes() {
        using var services = new ServiceCollection().AddBackflow((_, catalog) => catalog.Add<GenericNode<int>>("int-output"))
            .BuildServiceProvider();
        var graphs = services.GetRequiredService<BackflowGraphService>();
        Assert.True(graphs.TryBuild(new([new("one", "int-output")], []), out _, out _));
        Assert.False(graphs.Validate(new([new("one", typeof(GenericNode<string>).FullName!)], [])).IsValid);
    }

    [Fact]
    public void TryPrepareExecution_LiteralLimits_RejectsBeforeAdmission() {
        using var services = CreateServices(includeArray: true, configure: options => {
            options.MaximumArrayLength = 2;
            options.MaximumInputs = 1;
        });
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var submission = new BackflowExecutionSubmission(new([new("one", "array")], []),
            [new("one", "values", JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }))]);
        Assert.False(graphs.TryPrepareExecution(submission, out _, out var result));
        Assert.Equal(BackflowErrorCode.LimitExceeded, Assert.Single(result.Errors).Code);
        Assert.False(graphs.TryPrepareExecution(submission with { Inputs = [submission.Inputs[0], submission.Inputs[0]] }, out _, out _));
    }

    [Fact]
    public void TryBuild_RegisteredNodes_PreservesIdsAndDoesNotExecute() {
        using var services = CreateServices();
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var graph = ValidGraph();
        Assert.True(graphs.TryBuild(graph, out var pipeline, out var result));
        Assert.True(result.IsValid);
        Assert.Equal(["first", "second"], pipeline!.Value.Topology.Nodes.Select(node => node.ID).Order().ToArray());
        Assert.Single(pipeline.Value.Edges);
        Assert.Equal(0, services.GetRequiredService<NodeRegistry>().Get<NumberNode>()!.Executions);
    }

    [Fact]
    public void Validate_UnpublishedRegistryNode_RejectsEvenWithClrName() {
        using var services = CreateServices();
        var registry = services.GetRequiredService<NodeRegistry>();
        registry.Register(new TextNode());
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var graph = new BackflowGraph([new("attack", typeof(TextNode).AssemblyQualifiedName!)], []);
        Assert.Equal(BackflowErrorCode.UnknownNode, Assert.Single(graphs.Validate(graph).Errors).Code);
    }

    [Fact]
    public void Validate_ClientGenericArguments_CannotSelectTypes() {
        using var services = CreateServices();
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var graph = new BackflowGraph([new("attack", "System.Collections.Generic.List`1[System.IO.FileInfo]")], []);
        Assert.Equal(BackflowErrorCode.UnknownNode, Assert.Single(graphs.Validate(graph).Errors).Code);
    }

    [Fact]
    public void GetCatalog_Ports_UsesActualClrTypesAndGeneratedSchemas() {
        using var services = CreateServices();
        var descriptor = Assert.Single(services.GetRequiredService<BackflowGraphService>().GetCatalog().Nodes);
        Assert.Equal("number", descriptor.Id);
        var input = Assert.Single(descriptor.Ports, port => port.Direction == BackflowPortDirection.Input);
        Assert.Equal(typeof(int).FullName, input.ClrType);
        Assert.Contains("integer", input.Schema["type"]!.ToJsonString());
    }

    [Fact]
    public void Validate_BadDirectionsTypesDuplicateWritersAndCycles_Rejects() {
        using var services = CreateServices(includeText: true);
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var graph = ValidGraph();
        Assert.False(graphs.Validate(graph with { Edges = [new("first", "in", "second", "in")] }).IsValid);
        Assert.False(graphs.Validate(graph with { Nodes = [new("first", "number"), new("second", "text")] }).IsValid);
        Assert.False(graphs.Validate(graph with { Edges = [graph.Edges[0], graph.Edges[0]] }).IsValid);
        Assert.False(graphs.Validate(graph with { Edges = [graph.Edges[0], new("second", "out", "first", "in")] }).IsValid);
        Assert.False(graphs.Validate(graph with { Nodes = [new("first", "number"), new("first", "number")] }).IsValid);
        Assert.False(graphs.Validate(graph with { Edges = [new("missing", "out", "second", "in")] }).IsValid);
    }

    [Fact]
    public void Validate_NodeEdgeAndIdentifierBounds_RejectsBeforeBuilding() {
        using var services = CreateServices(configure: options => {
            options.MaximumNodes = 1;
            options.MaximumEdges = 1;
            options.MaximumIdentifierLength = 8;
        });
        var graphs = services.GetRequiredService<BackflowGraphService>();
        Assert.Equal(BackflowErrorCode.LimitExceeded, Assert.Single(graphs.Validate(ValidGraph()).Errors).Code);
        Assert.False(graphs.Validate(new([new("longidentifier", "number")], [])).IsValid);
        Assert.False(graphs.Validate(new(null!, [])).IsValid);
        Assert.False(graphs.Validate(new([null!], [])).IsValid);
    }

    [Fact]
    public void Validate_IndexedArrays_ChecksTypeCardinalityAndBounds() {
        using var services = CreateServices(includeArray: true, configure: options => options.MaximumArrayLength = 4);
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var graph = new BackflowGraph([new("first", "number"), new("second", "array")],
            [new("first", "out", "second", "values", 1)]);
        Assert.True(graphs.Validate(graph).IsValid);
        Assert.False(graphs.Validate(graph with { Edges = [graph.Edges[0] with { DestinationIndex = int.MaxValue }] }).IsValid);
        Assert.False(graphs.Validate(graph with { Edges = [graph.Edges[0] with { DestinationIndex = -1 }] }).IsValid);
        Assert.False(graphs.Validate(graph with { Edges = [graph.Edges[0] with { DestinationIndex = null }] }).IsValid);
    }

    [Fact]
    public void TryPrepareExecution_DeclaredInputType_CreatesIndependentContexts() {
        using var services = CreateServices();
        var graphs = services.GetRequiredService<BackflowGraphService>();
        var submission = new BackflowExecutionSubmission(new([new("one", "number")], []),
            [new("one", "in", JsonSerializer.SerializeToElement(42))]);
        Assert.True(graphs.TryPrepareExecution(submission, out var first, out _));
        Assert.True(graphs.TryPrepareExecution(submission, out var second, out _));
        Assert.NotSame(first!.Context, second!.Context);
        var node = Assert.Single(first.Pipeline.Topology.Nodes);
        Assert.Equal(42, first.Context.Read<int>(node.Mappings[node.Node.Inputs[0]]));
        Assert.False(graphs.TryPrepareExecution(submission with { Inputs = [new("one", "in", JsonSerializer.SerializeToElement("not an int"))] }, out _, out _));
        Assert.False(graphs.TryPrepareExecution(submission with { Inputs = [new("one", "out", JsonSerializer.SerializeToElement(3))] }, out _, out _));
        Assert.False(graphs.TryPrepareExecution(submission with { Inputs = [submission.Inputs[0], submission.Inputs[0]] }, out _, out _));
        Assert.False(graphs.TryPrepareExecution(new(ValidGraph(), [new("second", "in", JsonSerializer.SerializeToElement(1))]), out _, out _));
    }

    [Fact]
    public void Validate_CanceledRequest_ObservesCancellation() {
        using var services = CreateServices();
        var graphs = services.GetRequiredService<BackflowGraphService>();
        Assert.Throws<OperationCanceledException>(() => graphs.Validate(ValidGraph(), new CancellationToken(true)));
    }

    private static BackflowGraph ValidGraph() => new([new("first", "number"), new("second", "number")],
        [new("first", "out", "second", "in")]);

    private static ServiceProvider CreateServices(bool includeText = false, bool includeArray = false,
        Action<BackflowOptions>? configure = null) => new ServiceCollection().AddBackflow((_, catalog) => {
        catalog.Add<NumberNode>("number");
        if (includeText) catalog.Add<TextNode>("text");
        if (includeArray) catalog.Add<ArrayNode>("array");
    }, configure).BuildServiceProvider();

    private sealed class NumberNode : AbstractNode {
        public int Executions { get; private set; }
        public NumberNode() {
            Input(new InputPort<int>("in", 0, new PassAllPortValidator<int>()));
            Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Executions++;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class TextNode : AbstractNode {
        public TextNode() => Input(new InputPort<string>("in", null, new PassAllPortValidator<string>()));
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => ValueTask.FromResult(true);
    }

    private sealed class ArrayNode : AbstractNode {
        public ArrayNode() => Input(new ArrayInputPort<int>("values", 0, new PassAllPortValidator<int>(),
            new PassAllPortValidator<int[]>(), 1, 4));
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => ValueTask.FromResult(true);
    }

    private sealed class GenericNode<T> : AbstractNode {
        public GenericNode() => Output(new OutputPort<T>("value"));
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) => ValueTask.FromResult(true);
    }

    private sealed record SourceConfiguration(int Value);
    private sealed record SinkConfiguration(bool Enabled);
}
