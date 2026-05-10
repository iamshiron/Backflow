using System.Numerics;
using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Generic;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Serialization;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class GenericNodeTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class IntSourceNode : AbstractNode {
        public IOutputPort<int> Out { get; }
        public IntSourceNode() {
            Out = Output(new OutputPort<int>("Out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class DoubleSourceNode : AbstractNode {
        public IOutputPort<double> Out { get; }
        public DoubleSourceNode() {
            Out = Output(new OutputPort<double>("Out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class IntConsumerNode : AbstractNode {
        public IInputPort<int> In { get; }
        public IntConsumerNode() {
            In = Input(new InputPort<int>("In", 0, new PassValidator<int>()));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class TestGenericAddNode<T> : AbstractGenericNode where T : struct, INumber<T> {
        public IInputPort<T> A { get; }
        public IInputPort<T> B { get; }
        public IOutputPort<T> Sum { get; }

        public TestGenericAddNode() {
            A = Input(new NumericPortBuilder<T>(nameof(A)).Input());
            B = Input(new NumericPortBuilder<T>(nameof(B)).Input());
            Sum = Output(new NumericPortBuilder<T>(nameof(Sum)).Output());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Sum.Write(context, A.Read(context) + B.Read(context));
            return ValueTask.FromResult(true);
        }
    }

    private class TestGenericPassthroughNode<T> : AbstractGenericNode where T : struct, INumber<T> {
        public IInputPort<T> In { get; }
        public IOutputPort<T> Out { get; }

        public TestGenericPassthroughNode() {
            In = Input(new NumericPortBuilder<T>(nameof(In)).Input());
            Out = Output(new NumericPortBuilder<T>(nameof(Out)).Output());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Out.Write(context, In.Read(context));
            return ValueTask.FromResult(true);
        }
    }

    private class TestGenericNodeMultiType<T1, T2> : AbstractGenericNode
        where T1 : struct, INumber<T1>
        where T2 : struct, INumber<T2> {
        public IInputPort<T1> In1 { get; }
        public IInputPort<T2> In2 { get; }

        public TestGenericNodeMultiType() {
            In1 = Input(new NumericPortBuilder<T1>(nameof(In1)).Input());
            In2 = Input(new NumericPortBuilder<T2>(nameof(In2)).Input());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return ValueTask.FromResult(true);
        }
    }

    [Fact]
    public void BlueprintFactory_ExtractsTypeParameters() {
        var blueprint = BlueprintFactory.FromOpenType(typeof(TestGenericAddNode<>));

        Assert.Single(blueprint.TypeParameters);
        Assert.Equal("T", blueprint.TypeParameters[0].Name);
        Assert.Equal(0, blueprint.TypeParameters[0].Position);
        Assert.NotEmpty(blueprint.TypeParameters[0].Constraints);
    }

    [Fact]
    public void BlueprintFactory_ExtractsPorts() {
        var blueprint = BlueprintFactory.FromOpenType(typeof(TestGenericAddNode<>));

        Assert.Equal(3, blueprint.Ports.Length);

        var a = blueprint.GetPort("A");
        Assert.NotNull(a);
        Assert.Equal(PortDirection.Input, a!.Direction);
        Assert.Equal(0, a.TypeParameterIndex);

        var sum = blueprint.GetPort("Sum");
        Assert.NotNull(sum);
        Assert.Equal(PortDirection.Output, sum!.Direction);
        Assert.Equal(0, sum.TypeParameterIndex);
    }

    [Fact]
    public void BlueprintFactory_MultiTypeGeneric() {
        var blueprint = BlueprintFactory.FromOpenType(typeof(TestGenericNodeMultiType<,>));

        Assert.Equal(2, blueprint.TypeParameters.Length);
        Assert.Equal("T1", blueprint.TypeParameters[0].Name);
        Assert.Equal("T2", blueprint.TypeParameters[1].Name);

        Assert.Equal(2, blueprint.Ports.Length);

        var in1 = blueprint.GetPort("In1");
        Assert.NotNull(in1);
        Assert.Equal(0, in1!.TypeParameterIndex);

        var in2 = blueprint.GetPort("In2");
        Assert.NotNull(in2);
        Assert.Equal(1, in2!.TypeParameterIndex);
    }

    [Fact]
    public void BlueprintFactory_NonGenericType_Throws() {
        Assert.Throws<ArgumentException>(() =>
            BlueprintFactory.FromOpenType(typeof(IntSourceNode)));
    }

    [Fact]
    public void Blueprint_DisplayName_StripsBacktick() {
        var blueprint = BlueprintFactory.FromOpenType(typeof(TestGenericAddNode<>));
        Assert.Equal("TestGenericAddNode", blueprint.DisplayName);
    }

    [Fact]
    public void NodeRegistry_RegisterGeneric_ReturnsBlueprint() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));

        Assert.NotNull(blueprint);
        Assert.Equal(typeof(TestGenericAddNode<>), blueprint.OpenType);
    }

    [Fact]
    public void NodeRegistry_RegisterGeneric_NonGenericNode_Throws() {
        var registry = new NodeRegistry();
        Assert.Throws<ArgumentException>(() =>
            registry.RegisterGeneric(typeof(IntSourceNode)));
    }

    [Fact]
    public void NodeRegistry_RegisterGeneric_Duplicate_Throws() {
        var registry = new NodeRegistry();
        registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterGeneric(typeof(TestGenericAddNode<>)));
    }

    [Fact]
    public void NodeRegistry_GetBlueprint_ReturnsBlueprint() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));

        var retrieved = registry.GetBlueprint(typeof(TestGenericAddNode<>).FullName!);
        Assert.Same(blueprint, retrieved);
    }

    [Fact]
    public void NodeRegistry_GetOrCreateConcrete_InstantiatesGeneric() {
        var registry = new NodeRegistry();
        registry.RegisterGeneric(typeof(TestGenericAddNode<>));

        var node = registry.GetOrCreateConcrete(typeof(TestGenericAddNode<>), [typeof(int)]);
        Assert.IsType<TestGenericAddNode<int>>(node);
    }

    [Fact]
    public void NodeRegistry_GetOrCreateConcrete_CachesInstance() {
        var registry = new NodeRegistry();
        registry.RegisterGeneric(typeof(TestGenericAddNode<>));

        var node1 = registry.GetOrCreateConcrete(typeof(TestGenericAddNode<>), [typeof(int)]);
        var node2 = registry.GetOrCreateConcrete(typeof(TestGenericAddNode<>), [typeof(int)]);
        Assert.Same(node1, node2);
    }

    [Fact]
    public void NodeRegistry_GetOrCreateConcrete_DifferentTypes_DifferentInstances() {
        var registry = new NodeRegistry();
        registry.RegisterGeneric(typeof(TestGenericAddNode<>));

        var intNode = registry.GetOrCreateConcrete(typeof(TestGenericAddNode<>), [typeof(int)]);
        var doubleNode = registry.GetOrCreateConcrete(typeof(TestGenericAddNode<>), [typeof(double)]);
        Assert.NotSame(intNode, doubleNode);
        Assert.IsType<TestGenericAddNode<int>>(intNode);
        Assert.IsType<TestGenericAddNode<double>>(doubleNode);
    }

    [Fact]
    public void PipelineBuilder_AddNode_WithExplicitTypeArgs() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        var instance = builder.AddNode(blueprint, [typeof(int)]);
        Assert.NotNull(instance);
        Assert.NotNull(instance.Node);
        Assert.IsType<TestGenericAddNode<int>>(instance.Node);
    }

    [Fact]
    public void PipelineBuilder_AddNode_Deferred_ReturnsGenericNodeRef() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        var genericRef = builder.AddNode(blueprint);
        Assert.NotNull(genericRef);
        Assert.False(genericRef.IsResolved);
    }

    [Fact]
    public void PipelineBuilder_TypeInference_ConcreteToGeneric() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);
        var srcNode = new IntSourceNode();

        var source = builder.AddNode(srcNode);
        var genericRef = builder.AddNode(blueprint);

        builder.AddConnection(source, srcNode.Out, genericRef, genericRef.Port("A"));

        Assert.True(genericRef.IsResolved);
        Assert.Equal(typeof(int), genericRef.TypeArgs[0]);
    }

    [Fact]
    public void PipelineBuilder_Build_WithExplicitTypeArgs() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        builder.AddNode(blueprint, [typeof(int)]);
        var pipeline = builder.Build();

        Assert.Single(pipeline.Topology.Nodes);
        Assert.IsType<TestGenericAddNode<int>>(pipeline.Topology.Nodes.First().Node);
    }

    [Fact]
    public void PipelineBuilder_Build_WithTypeInference() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);
        var srcNode = new IntSourceNode();

        var source = builder.AddNode(srcNode);
        var genericRef = builder.AddNode(blueprint);

        builder.AddConnection(source, srcNode.Out, genericRef, genericRef.Port("A"));
        builder.AddConnection(source, srcNode.Out, genericRef, genericRef.Port("B"));

        var pipeline = builder.Build();

        Assert.Equal(2, pipeline.Topology.Nodes.Count());
        Assert.Single(pipeline.Topology.Nodes, n => n.Node is TestGenericAddNode<int>);
    }

    [Fact]
    public void PipelineBuilder_Build_UnresolvedGeneric_Throws() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        builder.AddNode(blueprint);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void PipelineBuilder_TypeInference_Chain_GenericToGeneric() {
        var registry = new NodeRegistry();
        var addBlueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var passBlueprint = registry.RegisterGeneric(typeof(TestGenericPassthroughNode<>));
        var builder = new PipelineBuilder(registry);
        var srcNode = new IntSourceNode();

        var source = builder.AddNode(srcNode);
        var addRef = builder.AddNode(addBlueprint);
        var passRef = builder.AddNode(passBlueprint);

        builder.AddConnection(source, srcNode.Out, addRef, addRef.Port("A"));
        builder.AddConnection(source, srcNode.Out, addRef, addRef.Port("B"));
        builder.AddConnection(addRef, addRef.Port("Sum"), passRef, passRef.Port("In"));

        var pipeline = builder.Build();

        Assert.Equal(3, pipeline.Topology.Nodes.Count());
        Assert.Single(pipeline.Topology.Nodes, n => n.Node is TestGenericAddNode<int>);
        Assert.Single(pipeline.Topology.Nodes, n => n.Node is TestGenericPassthroughNode<int>);
    }

    [Fact]
    public void GenericNode_Execution_ProducesCorrectResult() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        builder.AddNode(blueprint, [typeof(int)]);
        var pipeline = builder.Build();

        var context = new PipelineContext();
        var addInstance = pipeline.Topology.Nodes.ElementAt(0);

        var aPort = addInstance.Node.Ports.First(p => p.Name == "A");
        var bPort = addInstance.Node.Ports.First(p => p.Name == "B");

        context.Write(addInstance, aPort, 7);
        context.Write(addInstance, bPort, 5);

        var executor = new PipelineExecutor(pipeline);
        var stats = executor.Execute(context);

        Assert.Equal(1, stats.ExecutedNodes);
        Assert.Equal(0, stats.SkippedNodes);

        var sumPort = addInstance.Node.Ports.First(p => p.Name == "Sum");
        var result = context.Read<int>(addInstance, sumPort);
        Assert.Equal(12, result);
    }

    [Fact]
    public void GenericNode_DoubleType_Execution() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        builder.AddNode(blueprint, [typeof(double)]);
        var pipeline = builder.Build();

        var context = new PipelineContext();
        var addInstance = pipeline.Topology.Nodes.ElementAt(0);

        var aPort = addInstance.Node.Ports.First(p => p.Name == "A");
        var bPort = addInstance.Node.Ports.First(p => p.Name == "B");

        context.Write(addInstance, aPort, 3.14);
        context.Write(addInstance, bPort, 2.86);

        var executor = new PipelineExecutor(pipeline);
        executor.Execute(context);

        var sumPort = addInstance.Node.Ports.First(p => p.Name == "Sum");
        var result = context.Read<double>(addInstance, sumPort);
        Assert.Equal(6.0, result);
    }

    [Fact]
    public void PortType_IntPort_ReturnsInt() {
        var port = new InputPort<int>("test", 0, new PassValidator<int>());
        Assert.Equal(typeof(int), port.PortType);
    }

    [Fact]
    public void PortType_OutputPort_ReturnsType() {
        var port = new OutputPort<string>("test");
        Assert.Equal(typeof(string), port.PortType);
    }

    [Fact]
    public void PortType_GenericNodePorts_HaveCorrectTypes() {
        var registry = new NodeRegistry();
        var node = registry.GetOrCreateConcrete(typeof(TestGenericAddNode<>), [typeof(int)]);

        var aPort = node.Ports.First(p => p.Name == "A");
        var sumPort = node.Ports.First(p => p.Name == "Sum");

        Assert.Equal(typeof(int), aPort.PortType);
        Assert.Equal(typeof(int), sumPort.PortType);
    }

    [Fact]
    public void Serialization_GenericNode_RoundTrip() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);

        builder.AddNode(blueprint, [typeof(int)]);
        var pipeline = builder.Build();

        var json = pipeline.SerializeDefinition();
        Assert.Contains("System.Int32", json);

        var restored = PipelineSerialization.DeserializeDefinition(json, registry);
        Assert.Single(restored.Topology.Nodes);
        Assert.IsType<TestGenericAddNode<int>>(restored.Topology.Nodes.ElementAt(0).Node);
    }

    [Fact]
    public void Serialization_GenericNode_WithEdges_RoundTrip() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        registry.Register<IntSourceNode>();
        var builder = new PipelineBuilder(registry);
        var srcNode = new IntSourceNode();

        var source = builder.AddNode(srcNode);
        var add = builder.AddNode(blueprint, [typeof(int)]);

        builder.AddConnection(source, srcNode.Out, add,
            add.Node.Ports.First(p => p.Name == "A"));

        var pipeline = builder.Build();
        var json = pipeline.SerializeDefinition();
        var restored = PipelineSerialization.DeserializeDefinition(json, registry);

        Assert.Equal(2, restored.Topology.Nodes.Count());
        Assert.Single(restored.Edges);
    }

    [Fact]
    public void PipelineBuilder_MixedConcreteAndGeneric() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);
        var srcNode = new IntSourceNode();
        var consumerNode = new IntConsumerNode();

        var source = builder.AddNode(srcNode);
        var consumer = builder.AddNode(consumerNode);
        var add = builder.AddNode(blueprint, [typeof(int)]);

        builder.AddConnection(source, srcNode.Out, add,
            add.Node.Ports.First(p => p.Name == "A"));
        builder.AddConnection(add,
            add.Node.Ports.First(p => p.Name == "Sum"),
            consumer, consumerNode.In);

        var pipeline = builder.Build();
        Assert.Equal(3, pipeline.Topology.Nodes.Count());
        Assert.Equal(2, pipeline.Edges.Length);
    }

    [Fact]
    public void PipelineBuilder_CycleDetection_WithGenericNodes() {
        var registry = new NodeRegistry();
        var passBlueprint = registry.RegisterGeneric(typeof(TestGenericPassthroughNode<>));
        var builder = new PipelineBuilder(registry);
        var srcNode = new IntSourceNode();

        var source = builder.AddNode(srcNode);
        var pass1 = builder.AddNode(passBlueprint);
        var pass2 = builder.AddNode(passBlueprint);

        builder.AddConnection(source, srcNode.Out, pass1, pass1.Port("In"));
        builder.AddConnection(pass1, pass1.Port("Out"), pass2, pass2.Port("In"));

        Assert.Throws<PipelineCycleException>(() =>
            builder.AddConnection(pass2, pass2.Port("Out"), pass1, pass1.Port("In")));
    }

    [Fact]
    public void GenericNodeRef_Port_ReturnsBlueprintPort() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);
        var genericRef = builder.AddNode(blueprint);

        var portA = genericRef.Port("A");
        Assert.Equal("A", portA.Name);
        Assert.Equal(0, portA.TypeParameterIndex);
        Assert.Equal(PortDirection.Input, portA.Direction);
    }

    [Fact]
    public void GenericNodeRef_Port_InvalidName_Throws() {
        var registry = new NodeRegistry();
        var blueprint = registry.RegisterGeneric(typeof(TestGenericAddNode<>));
        var builder = new PipelineBuilder(registry);
        var genericRef = builder.AddNode(blueprint);

        Assert.Throws<ArgumentException>(() => genericRef.Port("NonExistent"));
    }

    [Fact]
    public void AbstractGenericNode_GetTypeArguments_ReturnsArgs() {
        var node = new TestGenericAddNode<int>();
        var args = node.GetTypeArguments();
        Assert.Single(args);
        Assert.Equal(typeof(int), args[0]);
    }
}
