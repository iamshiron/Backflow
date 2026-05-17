namespace Shiron.Lib.Pipeline.Types;

/// <summary>
/// <see cref="IStreamData"/> backed by a contiguous byte buffer. Provides <see cref="ReadOnlyMemory{T}"/> access
/// in addition to stream reads.
/// </summary>
public interface IBufferData : IStreamData {
    /// <summary>The underlying byte buffer.</summary>
    ReadOnlyMemory<byte> Data { get; }
}
