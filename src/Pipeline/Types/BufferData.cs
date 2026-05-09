namespace Shiron.Lib.Pipeline.Types;

public class BufferData(byte[] data) : IBufferData {
    public ReadOnlyMemory<byte> Data { get; } = data;
    public Stream OpenRead() {
        return new MemoryStream(data, false);
    }

    public void Dispose() { }
}
