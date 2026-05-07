namespace Shiron.Lib.Pipeline.Types;

public class MemoryBlob : IBlob, IImageBlob {
    public byte[] Data { get; set; } = [];
}
