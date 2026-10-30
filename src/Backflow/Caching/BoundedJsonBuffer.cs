using System.Buffers;

namespace Shiron.Backflow.Caching;

internal sealed class BoundedJsonBuffer(int limit, CancellationToken ct = default) : IBufferWriter<byte> {
    private const int WriterGrowthAllowance = 4096;
    private const int MaximumTokenExpansion = 6;
    private readonly int _maximumReservation = (int) Math.Min(Array.MaxLength, (long) limit * MaximumTokenExpansion + WriterGrowthAllowance);
    private byte[] _buffer = [];
    private int _written;
    private int _offered;

    internal ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public void Advance(int count) {
        ct.ThrowIfCancellationRequested();
        if (count < 0 || count > _offered) throw new ArgumentOutOfRangeException(nameof(count));
        if ((long) _written + count > limit) throw CapacityExceeded();
        _written += count;
        _offered = 0;
    }

    public Memory<byte> GetMemory(int sizeHint = 0) {
        ct.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        if (_written >= limit) throw CapacityExceeded();
        var requested = Math.Max(256, sizeHint);
        var required = (long) _written + requested;
        if (required > _maximumReservation) throw CapacityExceeded();
        if (required > _buffer.Length) {
            var capacity = (int) Math.Min(_maximumReservation, Math.Max(required, (long) _buffer.Length * 2));
            Array.Resize(ref _buffer, capacity);
        }
        _offered = requested;
        return _buffer.AsMemory(_written, requested);
    }

    public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    private static InvalidDataException CapacityExceeded() => new("Serialized cache data exceeds the configured byte budget.");
}
