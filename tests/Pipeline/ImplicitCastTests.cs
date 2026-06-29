using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Config;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class ImplicitCastTests {
    private class IntSourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;
        public IntSourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class DoubleDestNode : AbstractNode {
        public readonly IInputPort<double> In;
        public DoubleDestNode() {
            In = Input(new InputPort<double>("in", 0, new PassValidator<double>()));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class DoubleSourceNode : AbstractNode {
        public readonly IOutputPort<double> Out;
        public DoubleSourceNode() {
            Out = Output(new OutputPort<double>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class IntDestNode : AbstractNode {
        public readonly IInputPort<int> In;
        public IntDestNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class StringSourceNode : AbstractNode {
        public readonly IOutputPort<string> Out;
        public StringSourceNode() {
            Out = Output(new OutputPort<string>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class FloatDestNode : AbstractNode {
        public readonly IInputPort<float> In;
        public FloatDestNode() {
            In = Input(new InputPort<float>("in", 0, new PassValidator<float>()));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return new ValueTask<bool>(true);
        }
    }

    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void AddConnection_IntToDouble_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var src = builder.AddNode(new IntSourceNode());
        var dest = builder.AddNode(new DoubleDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void AddConnection_DoubleToInt_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var src = builder.AddNode(new DoubleSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void AddConnection_IncompatibleTypes_ThrowsTypeIncompatibilityException() {
        var builder = new PipelineBuilder(_registry);
        var src = builder.AddNode(new StringSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        var ex = Assert.Throws<TypeIncompatibilityException>(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Equal(typeof(string), ex.SourceType);
        Assert.Equal(typeof(int), ex.TargetType);
        Assert.Equal("out", ex.SourcePortName);
        Assert.Equal("in", ex.TargetPortName);
    }

    [Fact]
    public void AddConnection_SameType_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var src = builder.AddNode(new IntSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void Build_WithCompatibleTypes_Succeeds() {
        var builder = new PipelineBuilder(_registry);
        var src = builder.AddNode(new IntSourceNode());
        var dest = builder.AddNode(new DoubleDestNode());
        builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]);

        var pipeline = builder.Build();
        Assert.Single(pipeline.Edges);
    }

    [Fact]
    public void PipelineContext_Read_IntWrittenAsDouble() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.Equal(42.0, ctx.Read<double>(0));
    }

    [Fact]
    public void PipelineContext_Read_DoubleWrittenAsInt_Truncates() {
        var ctx = ArrayPipelineContext.Create(typeof(double));
        ctx.Write(0, 3.14);
        Assert.Equal(3, ctx.Read<int>(0));
    }

    [Fact]
    public void PipelineContext_Read_FloatToDouble() {
        var ctx = ArrayPipelineContext.Create(typeof(float));
        ctx.Write(0, 3.14f);
        Assert.Equal((double) 3.14f, ctx.Read<double>(0));
    }

    [Fact]
    public void PipelineContext_Read_LongToDouble() {
        var ctx = ArrayPipelineContext.Create(typeof(long));
        ctx.Write(0, (long) 1_000_000_000_000);
        Assert.Equal(1e12, ctx.Read<double>(0));
    }

    [Fact]
    public void PipelineContext_Has_ConsidersCastableTypes() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.True(ctx.Has<double>(0));
        Assert.True(ctx.Has<int>(0));
        Assert.True(ctx.Has<string>(0));
    }

    [Fact]
    public void PipelineContext_Read_IncompatibleTypes_ReturnsDefault() {
        var ctx = ArrayPipelineContext.Create(typeof(string));
        ctx.Write(0, "hello");
        Assert.Equal(0, ctx.Read<int>(0));
    }

    [Fact]
    public void PipelineContext_Read_ExactTypeMatch_NoConversionNeeded() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.Equal(42, ctx.Read<int>(0));
    }

    [Fact]
    public void PipelineContext_WithCustomRegistry_CustomCastWorks() {
        var registry = new CastRegistry();
        registry.Register<string, int>(TypeCast.Lossy, s => int.Parse(s));

        var ctx = ArrayPipelineContext.Create(registry, typeof(string));
        ctx.Write(0, "42");
        Assert.Equal(42, ctx.Read<int>(0));
    }

    [Fact]
    public void PipelineBuilder_WithCustomRegistry_CustomCastAccepted() {
        var registry = new CastRegistry();
        registry.Register<string, int>(TypeCast.Lossy, s => int.Parse(s));

        var builder = new PipelineBuilder(_registry, registry);
        var src = builder.AddNode(new StringSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void PipelineBuilder_WithCustomRegistry_IncompatibleStillRejected() {
        var registry = new CastRegistry();
        // No string→int registered

        var builder = new PipelineBuilder(_registry, registry);
        var src = builder.AddNode(new StringSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        Assert.Throws<TypeIncompatibilityException>(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
    }

    [Fact]
    public void PipelineContext_Read_ByteToDouble() {
        var ctx = ArrayPipelineContext.Create(typeof(byte));
        ctx.Write(0, (byte) 255);
        Assert.Equal(255.0, ctx.Read<double>(0));
    }

    [Fact]
    public void PipelineContext_Read_IntToFloat() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.Equal(42.0f, ctx.Read<float>(0));
    }

    [Fact]
    public void EndToEnd_IntToDouble_WritesAndReadsCorrectly() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new IntSourceNode();
        var destNode = new DoubleDestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        builder.AddConnection(src, srcNode.Out, dest, destNode.In);

        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        ctx.Write(src, srcNode.Out, 42);
        var result = ctx.Read<double>(dest, destNode.In);

        Assert.Equal(42.0, result);
    }

    [Fact]
    public void EndToEnd_DoubleToInt_Truncates() {
        var builder = new PipelineBuilder(_registry);
        var srcNode = new DoubleSourceNode();
        var destNode = new IntDestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        builder.AddConnection(src, srcNode.Out, dest, destNode.In);

        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        ctx.Write(src, srcNode.Out, 9.81);
        var result = ctx.Read<int>(dest, destNode.In);

        Assert.Equal(9, result);
    }

    [Fact]
    public void AddConnection_StrictTypeCasting_LossyCast_Throws() {
        var builder = new PipelineBuilder(_registry) {
            Config = new PipelineBuilderConfig { StrictTypeCasting = true },
        };
        var src = builder.AddNode(new DoubleSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        Assert.Throws<TypeIncompatibilityException>(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
    }

    [Fact]
    public void AddConnection_StrictTypeCasting_LosslessCast_Succeeds() {
        var builder = new PipelineBuilder(_registry) {
            Config = new PipelineBuilderConfig { StrictTypeCasting = true },
        };
        var src = builder.AddNode(new IntSourceNode());
        var dest = builder.AddNode(new DoubleDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void PipelineBuilder_RegisterCast_CustomDomainCast() {
        var builder = new PipelineBuilder(_registry)
            .RegisterCast<string, int>(TypeCast.Lossy, s => int.Parse(s));

        var src = builder.AddNode(new StringSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void PipelineBuilder_RegisterCast_FluentChaining() {
        var builder = new PipelineBuilder(_registry)
            .RegisterCast<string, int>(TypeCast.Lossy, s => int.Parse(s))
            .RegisterCast<int, string>(TypeCast.Lossless, v => v.ToString()!);

        var src = builder.AddNode(new StringSourceNode());
        var dest = builder.AddNode(new IntDestNode());

        var ex = Record.Exception(() =>
            builder.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
        Assert.Null(ex);
    }

    [Fact]
    public void PipelineBuilder_CreateContext_SharesCastRegistry() {
        var builder = new PipelineBuilder(_registry)
            .RegisterCast<string, int>(TypeCast.Lossy, s => int.Parse(s));

        var ctx = ArrayPipelineContext.Create(builder.CastRegistry, typeof(string));
        ctx.Write(0, "123");
        Assert.Equal(123, ctx.Read<int>(0));
    }

    [Fact]
    public void PipelineBuilder_RegisterCast_DoesNotAffectOtherBuilders() {
        var builder1 = new PipelineBuilder(_registry)
            .RegisterCast<string, int>(TypeCast.Lossy, s => int.Parse(s));
        var builder2 = new PipelineBuilder(_registry);

        var src = builder2.AddNode(new StringSourceNode());
        var dest = builder2.AddNode(new IntDestNode());

        Assert.Throws<TypeIncompatibilityException>(() =>
            builder2.AddConnection(src, src.Node.Ports[0], dest, dest.Node.Ports[0]));
    }

    [Fact]
    public void PipelineBuilder_RegisterCast_AppliesAtBuildAndRuntime() {
        var builder = new PipelineBuilder(_registry)
            .RegisterCast<string, int>(TypeCast.Lossy, s => int.Parse(s));

        var srcNode = new StringSourceNode();
        var destNode = new IntDestNode();
        var src = builder.AddNode(srcNode);
        var dest = builder.AddNode(destNode);
        builder.AddConnection(src, srcNode.Out, dest, destNode.In);

        var pipeline = builder.Build();
        var ctx = ArrayPipelineContext.ForPipeline(pipeline);

        ctx.Write(src, srcNode.Out, "99");
        var result = ctx.Read<int>(dest, destNode.In);

        Assert.Equal(99, result);
    }
}
