using Shiron.Lib.Pipeline.Context;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineContextTests {
    [Fact]
    public void Write_Overwrite_ReadReturnsLatestValue() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 1);
        ctx.Write(0, 2);
        Assert.Equal(2, ctx.Read<int>(0));
    }

    [Fact]
    public void Read_NonExistent_ReturnsDefault() {
        var ctx = ArrayPipelineContext.Create(typeof(int), typeof(int));
        Assert.Equal(0, ctx.Read<int>(1));
    }

    [Fact]
    public void HasAny_DistinguishesFromTypedHas() {
        var ctx = ArrayPipelineContext.Create(typeof(int));
        ctx.Write(0, 42);
        Assert.True(ctx.Has<int>(0));
        Assert.True(ctx.HasAny(0));
    }

    [Fact]
    public void Write_DifferentChannels_Isolated() {
        var ctx = ArrayPipelineContext.Create(typeof(int), typeof(int));
        ctx.Write(0, 10);
        ctx.Write(1, 20);
        Assert.Equal(10, ctx.Read<int>(0));
        Assert.Equal(20, ctx.Read<int>(1));
    }
}
