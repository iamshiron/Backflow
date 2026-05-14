namespace Shiron.Lib.Pipeline.Types;

public interface IBufferData : IStreamData {
    ReadOnlyMemory<byte> Data { get; }
}
