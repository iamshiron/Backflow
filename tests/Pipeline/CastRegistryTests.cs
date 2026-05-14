using Shiron.Lib.Pipeline.Casting;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class CastRegistryTests {
    [Fact]
    public void SameType_CanCast_ReturnsTrue() {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.CanCast(typeof(int), typeof(int)));
        Assert.True(registry.CanCast(typeof(double), typeof(double)));
    }

    [Fact]
    public void SameType_GetCastType_ReturnsNone() {
        var registry = CastRegistry.CreateDefault();
        Assert.Equal(TypeCast.None, registry.GetCastType(typeof(int), typeof(int)));
    }

    [Theory]
    [InlineData(typeof(byte), typeof(short), TypeCast.Lossless)]
    [InlineData(typeof(byte), typeof(int), TypeCast.Lossless)]
    [InlineData(typeof(byte), typeof(long), TypeCast.Lossless)]
    [InlineData(typeof(byte), typeof(float), TypeCast.Lossy)]
    [InlineData(typeof(byte), typeof(double), TypeCast.Lossless)]
    [InlineData(typeof(byte), typeof(decimal), TypeCast.Lossless)]
    [InlineData(typeof(short), typeof(int), TypeCast.Lossless)]
    [InlineData(typeof(short), typeof(long), TypeCast.Lossless)]
    [InlineData(typeof(short), typeof(float), TypeCast.Lossy)]
    [InlineData(typeof(short), typeof(double), TypeCast.Lossless)]
    [InlineData(typeof(short), typeof(decimal), TypeCast.Lossless)]
    [InlineData(typeof(int), typeof(long), TypeCast.Lossless)]
    [InlineData(typeof(int), typeof(float), TypeCast.Lossy)]
    [InlineData(typeof(int), typeof(double), TypeCast.Lossless)]
    [InlineData(typeof(int), typeof(decimal), TypeCast.Lossless)]
    [InlineData(typeof(long), typeof(float), TypeCast.Lossy)]
    [InlineData(typeof(long), typeof(double), TypeCast.Lossy)]
    [InlineData(typeof(long), typeof(decimal), TypeCast.Lossless)]
    [InlineData(typeof(float), typeof(double), TypeCast.Lossless)]
    public void Widening_Casts_AreClassifiedCorrectly(Type source, Type target, TypeCast expected) {
        var registry = CastRegistry.CreateDefault();
        Assert.Equal(expected, registry.GetCastType(source, target));
    }

    [Theory]
    [InlineData(typeof(int), typeof(byte))]
    [InlineData(typeof(int), typeof(short))]
    [InlineData(typeof(long), typeof(int))]
    [InlineData(typeof(long), typeof(short))]
    [InlineData(typeof(float), typeof(int))]
    [InlineData(typeof(float), typeof(long))]
    [InlineData(typeof(double), typeof(int))]
    [InlineData(typeof(double), typeof(long))]
    [InlineData(typeof(double), typeof(float))]
    [InlineData(typeof(decimal), typeof(int))]
    [InlineData(typeof(decimal), typeof(double))]
    public void Narrowing_Casts_AreLossy(Type source, Type target) {
        var registry = CastRegistry.CreateDefault();
        Assert.Equal(TypeCast.Lossy, registry.GetCastType(source, target));
    }

    [Theory]
    [InlineData(typeof(string), typeof(int))]
    [InlineData(typeof(bool), typeof(int))]
    [InlineData(typeof(string), typeof(double))]
    public void Incompatible_Types_CannotCast(Type source, Type target) {
        var registry = CastRegistry.CreateDefault();
        Assert.False(registry.CanCast(source, target));
    }

    [Theory]
    [InlineData(typeof(int), typeof(string))]
    [InlineData(typeof(bool), typeof(string))]
    [InlineData(typeof(double), typeof(string))]
    public void AnyType_CanCast_ToString(Type source, Type target) {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.CanCast(source, target));
        Assert.Equal(TypeCast.Lossless, registry.GetCastType(source, target));
    }

    [Fact]
    public void ToString_Cast_ConvertsCorrectly() {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.TryGetCast(typeof(int), typeof(string), out var rule));
        Assert.Equal("42", rule!.Cast(42));
    }

    [Fact]
    public void ToString_Cast_Null_ReturnsNull() {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.TryGetCast(typeof(object), typeof(string), out var rule));
        Assert.Null(rule!.Cast(null));
    }

    [Fact]
    public void ToString_Cast_StringToString_NotFound() {
        var registry = CastRegistry.CreateDefault();
        Assert.False(registry.TryGetCast(typeof(string), typeof(string), out _));
    }

    [Fact]
    public void TryGetCast_ReturnsFalse_ForUnknownPair() {
        var registry = CastRegistry.CreateDefault();
        Assert.False(registry.TryGetCast(typeof(string), typeof(int), out _));
    }

    [Fact]
    public void Converter_IntToDouble_ReturnsCorrectValue() {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.TryGetCast(typeof(int), typeof(double), out var rule));
        Assert.Equal(42.0, rule!.Cast(42));
    }

    [Fact]
    public void Converter_DoubleToInt_Truncates() {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.TryGetCast(typeof(double), typeof(int), out var rule));
        Assert.Equal(3, rule!.Cast(3.14));
    }

    [Fact]
    public void Converter_LongToDouble_WorksForLargeValue() {
        var registry = CastRegistry.CreateDefault();
        Assert.True(registry.TryGetCast(typeof(long), typeof(double), out var rule));
        Assert.Equal(1e18, rule!.Cast((long) 1e18));
    }

    [Fact]
    public void Register_CustomCast_Succeeds() {
        var registry = new CastRegistry();
        registry.Register<string, int>(TypeCast.Lossy, s => int.Parse(s));

        Assert.True(registry.CanCast(typeof(string), typeof(int)));
        Assert.Equal(TypeCast.Lossy, registry.GetCastType(typeof(string), typeof(int)));
    }

    [Fact]
    public void Register_CustomCast_ConverterWorks() {
        var registry = new CastRegistry();
        registry.Register<string, int>(TypeCast.Lossy, s => int.Parse(s));

        registry.TryGetCast(typeof(string), typeof(int), out var rule);
        Assert.Equal(42, rule!.Cast("42"));
    }

    [Fact]
    public void Register_OverwritesExisting() {
        var registry = CastRegistry.CreateDefault();
        Assert.Equal(TypeCast.Lossless, registry.GetCastType(typeof(int), typeof(double)));

        registry.Register<int, double>(TypeCast.Lossy, v => (double) v);
        Assert.Equal(TypeCast.Lossy, registry.GetCastType(typeof(int), typeof(double)));
    }

    [Fact]
    public void Register_Fluent() {
        var registry = new CastRegistry()
            .Register<string, int>(TypeCast.Lossy, s => int.Parse(s))
            .Register<int, string>(TypeCast.Lossless, v => v.ToString()!);

        Assert.True(registry.CanCast(typeof(string), typeof(int)));
        Assert.True(registry.CanCast(typeof(int), typeof(string)));
    }

    [Fact]
    public void Default_IsPopulated() {
        Assert.True(CastRegistry.Default.CanCast(typeof(int), typeof(double)));
        Assert.True(CastRegistry.Default.CanCast(typeof(double), typeof(int)));
    }
}
