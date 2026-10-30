using System.Text;
using System.Text.Json;
using Shiron.Backflow.Caching;
using Xunit;

namespace Shiron.Backflow.Tests;

public sealed class BoundedJsonBufferTests {
    [Fact]
    public void Advance_OverBudget_RejectsWithoutCommittingBytes() {
        var buffer = new BoundedJsonBuffer(16);
        buffer.GetSpan(32).Fill(1);
        Assert.Throws<InvalidDataException>(() => buffer.Advance(17));
        Assert.Equal(0, buffer.WrittenSpan.Length);
    }

    [Fact]
    public void GetMemory_OversizedTokenReservation_RejectsWithoutAllocatingTokenBuffer() {
        var buffer = new BoundedJsonBuffer(16);
        Assert.Throws<InvalidDataException>(() => buffer.GetMemory(int.MaxValue));
        Assert.Equal(0, buffer.WrittenSpan.Length);
    }

    [Fact]
    public void Serialize_StringWithinExactByteBudget_AllowsConservativeUtf8WriterReservation() {
        var expected = "\"" + new string('a', 1000) + "\"";
        var buffer = new BoundedJsonBuffer(Encoding.UTF8.GetByteCount(expected));
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStringValue(new string('a', 1000));
            writer.Flush();
        }
        Assert.Equal(expected, Encoding.UTF8.GetString(buffer.WrittenSpan));
    }
}
