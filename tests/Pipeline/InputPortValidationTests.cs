using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class InputPortValidationTests {
    private static (NodeContext ctx, Guid channelId) CreateContext() {
        var pipeline = new PipelineContext();
        var channelId = Guid.NewGuid();
        return (new NodeContext(pipeline, new Dictionary<IPort, Guid>()), channelId);
    }

    private static NodeContext WriteToChannel<T>(InputPort<T> port, T value) {
        var pipeline = new PipelineContext();
        var channelId = Guid.NewGuid();
        var mappings = new Dictionary<IPort, Guid> { [port] = channelId };
        pipeline.Write(channelId, value);
        return new NodeContext(pipeline, mappings);
    }

    private static InputPort<int> CreateNumericPort(int? min = null, int? max = null, int? defaultVal = null, bool nullable = false) {
        var builder = new NumericPortBuilder<int>("num");
        if (min.HasValue) builder.Min(min.Value);
        if (max.HasValue) builder.Max(max.Value);
        if (defaultVal.HasValue) builder.Default(defaultVal.Value);
        if (nullable) builder.Nullable();
        return (InputPort<int>) builder.Input();
    }

    private static InputPort<string> CreateStringPort(int? minLength = null, int? maxLength = null, string? defaultVal = null, bool nullable = false) {
        var builder = new StringPortBuilder("str");
        if (minLength.HasValue) builder.MinLength(minLength.Value);
        if (maxLength.HasValue) builder.MaxLength(maxLength.Value);
        if (defaultVal is not null) builder.Default(defaultVal);
        if (nullable) builder.Nullable();
        return (InputPort<string>) builder.Input();
    }

    [Fact]
    public void Read_ValidNumericValue_ReturnsValue() {
        var port = CreateNumericPort(min: 0, max: 100);
        var ctx = WriteToChannel(port, 42);
        Assert.Equal(42, port.Read(ctx));
    }

    [Fact]
    public void Read_InvalidNumericValue_ThrowsPortValidationException() {
        var port = CreateNumericPort(min: 0, max: 100);
        var ctx = WriteToChannel(port, 200);
        var ex = Assert.Throws<PortValidationException>(() => port.Read(ctx));
        Assert.Equal("num", ex.PortName);
        Assert.Equal(200, ex.Value);
        Assert.Contains("exceeds maximum", ex.Error);
    }

    [Fact]
    public void Read_NumericBelowMin_ThrowsPortValidationException() {
        var port = CreateNumericPort(min: 10, max: 100);
        var ctx = WriteToChannel(port, 5);
        var ex = Assert.Throws<PortValidationException>(() => port.Read(ctx));
        Assert.Contains("below minimum", ex.Error);
    }

    [Fact]
    public void Read_ValidStringValue_ReturnsValue() {
        var port = CreateStringPort(minLength: 1, maxLength: 10);
        var ctx = WriteToChannel(port, "hello");
        Assert.Equal("hello", port.Read(ctx));
    }

    [Fact]
    public void Read_StringTooLong_ThrowsPortValidationException() {
        var port = CreateStringPort(maxLength: 5);
        var ctx = WriteToChannel(port, "hello world");
        var ex = Assert.Throws<PortValidationException>(() => port.Read(ctx));
        Assert.Equal("str", ex.PortName);
        Assert.Contains("exceeds maximum", ex.Error);
    }

    [Fact]
    public void Read_StringTooShort_ThrowsPortValidationException() {
        var port = CreateStringPort(minLength: 5);
        var ctx = WriteToChannel(port, "hi");
        var ex = Assert.Throws<PortValidationException>(() => port.Read(ctx));
        Assert.Contains("below minimum", ex.Error);
    }

    [Fact]
    public void TryRead_ValidValue_ReturnsTrueAndValue() {
        var port = CreateNumericPort(min: 0, max: 100);
        var ctx = WriteToChannel(port, 42);
        var result = port.TryRead(ctx, out var value);
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryRead_InvalidValue_ThrowsPortValidationException() {
        var port = CreateNumericPort(max: 10);
        var ctx = WriteToChannel(port, 99);
        Assert.Throws<PortValidationException>(() => port.TryRead(ctx, out _));
    }

    [Fact]
    public void TryRead_NoValueInContext_ReturnsFalseAndDefault() {
        var port = CreateNumericPort(min: 0, max: 100, defaultVal: 7);
        var pipeline = new PipelineContext();
        var mappings = new Dictionary<IPort, Guid> { [port] = Guid.NewGuid() };
        var ctx = new NodeContext(pipeline, mappings);
        var result = port.TryRead(ctx, out var value);
        Assert.False(result);
        Assert.Equal(7, value);
    }

    [Fact]
    public void ReadAny_ValidValue_ReturnsValue() {
        var port = CreateNumericPort(min: 0, max: 100);
        var ctx = WriteToChannel(port, 42);
        var value = port.ReadAny(ctx);
        Assert.Equal(42, value);
    }

    [Fact]
    public void ReadAny_InvalidValue_ThrowsPortValidationException() {
        var port = CreateNumericPort(max: 10);
        var ctx = WriteToChannel(port, 99);
        Assert.Throws<PortValidationException>(() => port.ReadAny(ctx));
    }

    [Fact]
    public void Read_BoundaryMinValue_ReturnsValue() {
        var port = CreateNumericPort(min: 0, max: 100);
        var ctx = WriteToChannel(port, 0);
        Assert.Equal(0, port.Read(ctx));
    }

    [Fact]
    public void Read_BoundaryMaxValue_ReturnsValue() {
        var port = CreateNumericPort(min: 0, max: 100);
        var ctx = WriteToChannel(port, 100);
        Assert.Equal(100, port.Read(ctx));
    }

    [Fact]
    public void Read_ExactMinLengthString_ReturnsValue() {
        var port = CreateStringPort(minLength: 5, maxLength: 10);
        var ctx = WriteToChannel(port, "hello");
        Assert.Equal("hello", port.Read(ctx));
    }

    [Fact]
    public void Read_ExactMaxLengthString_ReturnsValue() {
        var port = CreateStringPort(minLength: 1, maxLength: 5);
        var ctx = WriteToChannel(port, "hello");
        Assert.Equal("hello", port.Read(ctx));
    }
}

public class PortValidationExceptionTests {
    [Fact]
    public void Constructor_SetsPropertiesCorrectly() {
        var ex = new PortValidationException("myPort", 42, "Value out of range");
        Assert.Equal("myPort", ex.PortName);
        Assert.Equal(42, ex.Value);
        Assert.Equal("Value out of range", ex.Error);
    }

    [Fact]
    public void Constructor_IncludesPortNameInMessage() {
        var ex = new PortValidationException("myPort", 42, "some error");
        Assert.Contains("myPort", ex.Message);
    }

    [Fact]
    public void Constructor_IncludesErrorInMessage() {
        var ex = new PortValidationException("myPort", 42, "some error");
        Assert.Contains("some error", ex.Message);
    }

    [Fact]
    public void Constructor_IncludesValueInMessage() {
        var ex = new PortValidationException("myPort", 42, "some error");
        Assert.Contains("42", ex.Message);
    }

    [Fact]
    public void Constructor_NullValue_MessageContainsNull() {
        var ex = new PortValidationException("port", null, "bad value");
        Assert.Contains("bad value", ex.Message);
    }
}
