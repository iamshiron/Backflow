using Shiron.Lib.Pipeline.Caching;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class CacheEntryTests {
    [Fact]
    public void CachedAt_Default_IsUtcNow() {
        var before = DateTime.UtcNow;
        var entry = new CacheEntry();
        var after = DateTime.UtcNow;

        Assert.True(entry.CachedAt >= before);
        Assert.True(entry.CachedAt <= after);
    }

    [Fact]
    public void CachedAt_Custom_SetsValue() {
        var customTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new CacheEntry(customTime);
        Assert.Equal(customTime, entry.CachedAt);
    }

    [Fact]
    public void AddInput_StoresInput() {
        var entry = new CacheEntry();
        entry.AddInput("port1", typeof(int), 42);

        Assert.Single(entry.Inputs);
        Assert.Equal("port1", entry.Inputs[0].PortName);
        Assert.Equal(typeof(int).FullName, entry.Inputs[0].TypeName);
        Assert.Equal(42, entry.Inputs[0].Value);
    }

    [Fact]
    public void AddInput_MultipleInputs_AllStored() {
        var entry = new CacheEntry();
        entry.AddInput("a", typeof(int), 1);
        entry.AddInput("b", typeof(string), "hello");

        Assert.Equal(2, entry.Inputs.Count);
        Assert.Equal("a", entry.Inputs[0].PortName);
        Assert.Equal("b", entry.Inputs[1].PortName);
    }

    [Fact]
    public void AddOutput_Generic_StoresOutput() {
        var entry = new CacheEntry();
        entry.AddOutput("out1", 42);

        Assert.Single(entry.Outputs);
        Assert.Equal("out1", entry.Outputs[0].PortName);
        Assert.Equal(42, entry.Outputs[0].Value);
    }

    [Fact]
    public void AddOutput_NonGeneric_StoresOutput() {
        var entry = new CacheEntry();
        entry.AddOutput("out1", typeof(int), 42);

        Assert.Single(entry.Outputs);
        Assert.Equal("out1", entry.Outputs[0].PortName);
        Assert.Equal(typeof(int).FullName, entry.Outputs[0].TypeName);
        Assert.Equal(42, entry.Outputs[0].Value);
    }

    [Fact]
    public void GetOutput_Generic_ReturnsValue() {
        var entry = new CacheEntry();
        entry.AddOutput("out1", 42);
        Assert.Equal(42, entry.GetOutput<int>("out1"));
    }

    [Fact]
    public void GetOutput_Generic_NonExistent_ReturnsDefault() {
        var entry = new CacheEntry();
        Assert.Equal(0, entry.GetOutput<int>("nonexistent"));
    }

    [Fact]
    public void GetOutputAny_ReturnsValue() {
        var entry = new CacheEntry();
        entry.AddOutput("out1", typeof(string), "hello");
        Assert.Equal("hello", entry.GetOutputAny("out1"));
    }

    [Fact]
    public void GetOutputAny_NonExistent_ReturnsNull() {
        var entry = new CacheEntry();
        Assert.Null(entry.GetOutputAny("nonexistent"));
    }

    [Fact]
    public void HasOutput_ExistingPort_ReturnsTrue() {
        var entry = new CacheEntry();
        entry.AddOutput("out1", 42);
        Assert.True(entry.HasOutput("out1"));
    }

    [Fact]
    public void HasOutput_NonExistentPort_ReturnsFalse() {
        var entry = new CacheEntry();
        Assert.False(entry.HasOutput("nonexistent"));
    }

    [Fact]
    public void OutputTypeOf_ExistingPort_ReturnsType() {
        var entry = new CacheEntry();
        entry.AddOutput("out1", typeof(int), 42);
        Assert.Equal(typeof(int), entry.OutputTypeOf("out1"));
    }

    [Fact]
    public void OutputTypeOf_NonExistentPort_ReturnsNull() {
        var entry = new CacheEntry();
        Assert.Null(entry.OutputTypeOf("nonexistent"));
    }

}
