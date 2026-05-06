using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class BasePortBuilderTests {
    [Fact]
    public void Optional_SetsIsNotRequired() {
        var builder = new NumericPortBuilder<int>("test");
        builder.Optional();
        Assert.False(builder.IsRequired);
    }

    [Fact]
    public void Optional_False_KeepsRequired() {
        var builder = new NumericPortBuilder<int>("test");
        builder.Optional(false);
        Assert.True(builder.IsRequired);
    }

    [Fact]
    public void Nullable_SetsIsNullable() {
        var builder = new NumericPortBuilder<int>("test");
        builder.Nullable();
        Assert.True(builder.IsNullable);
    }

    [Fact]
    public void Nullable_False_KeepsNotNullable() {
        var builder = new NumericPortBuilder<int>("test");
        builder.Nullable(false);
        Assert.False(builder.IsNullable);
    }

    [Fact]
    public void Default_SetsDefaultValue() {
        var builder = new NumericPortBuilder<int>("test");
        builder.Default(42);
        Assert.Equal(42, builder.DefaultValue);
    }

    [Fact]
    public void Input_RequiredNonNullPort_ReturnsInputPort() {
        var builder = new NumericPortBuilder<int>("test");
        var port = builder.Input();
        Assert.NotNull(port);
        Assert.Equal("test", port.Name);
    }

    [Fact]
    public void Input_OptionalWithoutDefault_ThrowsInvalidOperationException() {
        var builder = new StringPortBuilder("test");
        builder.Optional();
        Assert.Throws<InvalidOperationException>(() => builder.Input());
    }

    [Fact]
    public void Input_OptionalWithDefault_ReturnsInputPort() {
        var builder = new NumericPortBuilder<int>("test");
        builder.Optional().Default(10);
        var port = builder.Input();
        Assert.NotNull(port);
    }

    [Fact]
    public void Input_NullableAndOptional_ReturnsInputPort() {
        var builder = new StringPortBuilder("test");
        builder.Nullable().Optional();
        var port = builder.Input();
        Assert.NotNull(port);
    }

    [Fact]
    public void Output_ReturnsOutputPort() {
        var builder = new NumericPortBuilder<int>("test");
        var port = builder.Output();
        Assert.NotNull(port);
        Assert.Equal("test", port.Name);
    }
}
