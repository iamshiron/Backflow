using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Types;

/// <summary>
/// In-memory <see cref="IBufferData"/> backed by a byte array. Provides a <see cref="ReadOnlyMemory{T}"/> view
/// and a non-owning <see cref="MemoryStream"/> for reads.
/// </summary>
public class BufferData(byte[] data) : IBufferData {
    public ReadOnlyMemory<byte> Data { get; } = data;
    public Stream OpenRead() {
        return new MemoryStream(data, false);
    }

    public void Dispose() { }
}
