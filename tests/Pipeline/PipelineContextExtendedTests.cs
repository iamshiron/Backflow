using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineContextWriteAtTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class ArrayNode : AbstractNode {
        public readonly IArrayInputPort<int> Data;

        public ArrayNode() {
            Data = Input(new ArrayInputPort<int>(
                "data", 0, new PassValidator<int>(), new PassAllArrayValidator(), 0, null
            ));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    private class PassAllArrayValidator : IPortValidator<int[]> {
        public string? Validate(int[]? value) => null;
    }

    [Fact]
    public void WriteAt_NoExistingArray_CreatesArrayOfIndexPlusOne() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node);
        var ctx = new PipelineContext();

        ctx.WriteAt(instance, node.Data, 3, 99);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(4, array.Length);
        Assert.Equal(99, array[3]);
    }

    [Fact]
    public void WriteAt_ExistingArray_SetsElementAtIndex() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node);
        var ctx = new PipelineContext();

        ctx.WriteAt(instance, node.Data, 0, 10);
        ctx.WriteAt(instance, node.Data, 1, 20);
        ctx.WriteAt(instance, node.Data, 2, 30);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal([10, 20, 30], array!);
    }

    [Fact]
    public void WriteAt_IndexBeyondArrayLength_ResizesArray() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node);
        var ctx = new PipelineContext();

        ctx.WriteAt(instance, node.Data, 0, 1);
        ctx.WriteAt(instance, node.Data, 5, 6);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(6, array!.Length);
        Assert.Equal(1, array[0]);
        Assert.Equal(6, array[5]);
        Assert.Equal(0, array[1]);
    }

    [Fact]
    public void WriteAt_FrozenPort_UsesFrozenCountAsInitialSize() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node, new Dictionary<string, int> { ["data"] = 4 });
        var ctx = new PipelineContext();

        ctx.WriteAt(instance, node.Data, 2, 42);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(4, array!.Length);
        Assert.Equal(42, array[2]);
    }

    [Fact]
    public void WriteAt_OverwriteIndex_UpdatesValue() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node);
        var ctx = new PipelineContext();

        ctx.WriteAt(instance, node.Data, 0, 10);
        ctx.WriteAt(instance, node.Data, 0, 99);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(99, array![0]);
    }

    [Fact]
    public void WriteAt_IndexZero_CreatesSingleElementArray() {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        var node = new ArrayNode();
        var instance = builder.AddNode(node);
        var ctx = new PipelineContext();

        ctx.WriteAt(instance, node.Data, 0, 7);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Single(array!);
        Assert.Equal(7, array[0]);
    }
}

public class PipelineContextCastOnReadTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;

        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context)
            => ValueTask.FromResult(true);
    }

    [Fact]
    public void Read_IntStoredAsDouble_ReturnsDoubleViaCast() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 42);

        var result = ctx.Read<double>(id);
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void Read_LongStoredAsDouble_ReturnsDoubleViaCast() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 123456789L);

        var result = ctx.Read<double>(id);
        Assert.Equal(123456789.0, result);
    }

    [Fact]
    public void Read_FloatStoredAsDouble_ReturnsDoubleViaCast() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 3.14f);

        var result = ctx.Read<double>(id);
        Assert.Equal((double) 3.14f, result);
    }

    [Fact]
    public void Read_DoubleStoredAsInt_TruncatesViaCast() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 3.14);

        var result = ctx.Read<int>(id);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Read_ByteStoredAsDouble_WidensViaCast() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, (byte) 255);

        var result = ctx.Read<double>(id);
        Assert.Equal(255.0, result);
    }

    [Fact]
    public void Read_IncompatibleTypes_ReturnsDefault() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, "hello");

        var result = ctx.Read<int>(id);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Has_CastableType_ReturnsTrue() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 42);

        Assert.True(ctx.Has<double>(id));
    }

    [Fact]
    public void Has_ExactType_ReturnsTrue() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 42);

        Assert.True(ctx.Has<int>(id));
    }

    [Fact]
    public void Has_IncompatibleType_ReturnsFalse() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, "hello");

        Assert.False(ctx.Has<int>(id));
    }

    [Fact]
    public void Read_CustomCastRegistry_AppliesCustomCast() {
        var customRegistry = new CastRegistry();
        customRegistry.Register<string, int>(TypeCast.Lossless, s => int.Parse(s!));

        var ctx = new PipelineContext(customRegistry);
        var id = Guid.NewGuid();

        ctx.Write(id, "123");

        var result = ctx.Read<int>(id);
        Assert.Equal(123, result);
    }

    [Fact]
    public void Read_NullStoredValue_WithCastRule_ReturnsDefault() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write<int>(id, default);

        var result = ctx.Read<double>(id);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Read_IntStoredAsFloat_ReturnsFloatViaCast() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();

        ctx.Write(id, 42);

        var result = ctx.Read<float>(id);
        Assert.Equal(42.0f, result);
    }
}
