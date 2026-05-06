using Shiron.Lib.Pipeline.Context;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class PipelineContextTests {
    [Fact]
    public void Write_Overwrite_ReadReturnsLatestValue() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();
        ctx.Write(id, 1);
        ctx.Write(id, 2);
        Assert.Equal(2, ctx.Read<int>(id));
    }

    [Fact]
    public void Read_NonExistent_ReturnsDefault() {
        var ctx = new PipelineContext();
        Assert.Equal(0, ctx.Read<int>(Guid.NewGuid()));
    }

    [Fact]
    public void HasAny_DistinguishesFromTypedHas() {
        var ctx = new PipelineContext();
        var id = Guid.NewGuid();
        ctx.Write(id, 42);
        Assert.True(ctx.Has<int>(id));
        Assert.True(ctx.HasAny(id));
    }

    [Fact]
    public void Write_DifferentGuids_Isolated() {
        var ctx = new PipelineContext();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        ctx.Write(idA, 10);
        ctx.Write(idB, 20);
        Assert.Equal(10, ctx.Read<int>(idA));
        Assert.Equal(20, ctx.Read<int>(idB));
    }
}
