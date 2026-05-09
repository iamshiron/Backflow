namespace Shiron.Lib.Samples.Pipeline.Types;

public interface IBufferData : IStreamData {
    ReadOnlyMemory<byte> Data { get; }
}
