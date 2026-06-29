using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Casting;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Registry;
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

    private static ArrayPipelineContext BuildContext(ArrayNode node, out PipelineBuilder.NodeInstance instance) {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        instance = builder.AddNode(node);
        var pipeline = builder.Build();
        return ArrayPipelineContext.ForPipeline(pipeline);
    }

    private static ArrayPipelineContext BuildContext(ArrayNode node, Dictionary<string, int> arrayCounts, out PipelineBuilder.NodeInstance instance) {
        var registry = new NodeRegistry();
        var builder = new PipelineBuilder(registry);
        instance = builder.AddNode(node, arrayCounts);
        var pipeline = builder.Build();
        return ArrayPipelineContext.ForPipeline(pipeline);
    }

    [Fact]
    public void WriteAt_NoExistingArray_CreatesArrayOfIndexPlusOne() {
        var node = new ArrayNode();
        var ctx = BuildContext(node, out var instance);

        ctx.WriteAt(instance, node.Data, 3, 99);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(4, array.Length);
        Assert.Equal(99, array[3]);
    }

    [Fact]
    public void WriteAt_ExistingArray_SetsElementAtIndex() {
        var node = new ArrayNode();
        var ctx = BuildContext(node, out var instance);

        ctx.WriteAt(instance, node.Data, 0, 10);
        ctx.WriteAt(instance, node.Data, 1, 20);
        ctx.WriteAt(instance, node.Data, 2, 30);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal([10, 20, 30], array!);
    }

    [Fact]
    public void WriteAt_IndexBeyondArrayLength_ResizesArray() {
        var node = new ArrayNode();
        var ctx = BuildContext(node, out var instance);

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
        var node = new ArrayNode();
        var ctx = BuildContext(node, new Dictionary<string, int> { ["data"] = 4 }, out var instance);

        ctx.WriteAt(instance, node.Data, 2, 42);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(4, array!.Length);
        Assert.Equal(42, array[2]);
    }

    [Fact]
    public void WriteAt_OverwriteIndex_UpdatesValue() {
        var node = new ArrayNode();
        var ctx = BuildContext(node, out var instance);

        ctx.WriteAt(instance, node.Data, 0, 10);
        ctx.WriteAt(instance, node.Data, 0, 99);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Equal(99, array![0]);
    }

    [Fact]
    public void WriteAt_IndexZero_CreatesSingleElementArray() {
        var node = new ArrayNode();
        var ctx = BuildContext(node, out var instance);

        ctx.WriteAt(instance, node.Data, 0, 7);

        var array = ctx.Read<int[]>(instance, (IPort) node.Data);
        Assert.NotNull(array);
        Assert.Single(array!);
        Assert.Equal(7, array[0]);
    }
}

public class PipelineContextCastOnReadTests {
    [Fact]
    public void Read_IntStoredAsDouble_ReturnsDoubleViaCast() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.Equal(42.0, ctx.Read<double>(0));
    }

    [Fact]
    public void Read_LongStoredAsDouble_ReturnsDoubleViaCast() {
        var ctx = ArrayPipelineContext.Create(typeof(long));
        ctx.Write(0, 123456789L);
        Assert.Equal(123456789.0, ctx.Read<double>(0));
    }

    [Fact]
    public void Read_FloatStoredAsDouble_ReturnsDoubleViaCast() {
        var ctx = ArrayPipelineContext.Create(typeof(float));
        ctx.Write(0, 3.14f);
        Assert.Equal((double) 3.14f, ctx.Read<double>(0));
    }

    [Fact]
    public void Read_DoubleStoredAsInt_TruncatesViaCast() {
        var ctx = ArrayPipelineContext.Create(typeof(double));
        ctx.Write(0, 3.14);
        Assert.Equal(3, ctx.Read<int>(0));
    }

    [Fact]
    public void Read_ByteStoredAsDouble_WidensViaCast() {
        var ctx = ArrayPipelineContext.Create(typeof(byte));
        ctx.Write(0, (byte) 255);
        Assert.Equal(255.0, ctx.Read<double>(0));
    }

    [Fact]
    public void Read_IncompatibleTypes_ReturnsDefault() {
        var ctx = ArrayPipelineContext.Create(typeof(string));
        ctx.Write(0, "hello");
        Assert.Equal(0, ctx.Read<int>(0));
    }

    [Fact]
    public void Has_CastableType_ReturnsTrue() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.True(ctx.Has<double>(0));
    }

    [Fact]
    public void Has_ExactType_ReturnsTrue() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.True(ctx.Has<int>(0));
    }

    [Fact]
    public void Has_IncompatibleType_ReturnsFalse() {
        var ctx = ArrayPipelineContext.Create(typeof(string));
        ctx.Write(0, "hello");
        Assert.False(ctx.Has<int>(0));
    }

    [Fact]
    public void Read_CustomCastRegistry_AppliesCustomCast() {
        var customRegistry = new CastRegistry();
        customRegistry.Register<string, int>(TypeCast.Lossless, s => int.Parse(s!));

        var ctx = ArrayPipelineContext.Create(customRegistry, typeof(string));
        ctx.Write(0, "123");
        Assert.Equal(123, ctx.Read<int>(0));
    }

    [Fact]
    public void Read_NullStoredValue_WithCastRule_ReturnsDefault() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write<int>(0, default);
        Assert.Equal(0.0, ctx.Read<double>(0));
    }

    [Fact]
    public void Read_IntStoredAsFloat_ReturnsFloatViaCast() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.Equal(42.0f, ctx.Read<float>(0));
    }
}
