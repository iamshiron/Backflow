namespace Shiron.Lib.Pipeline.Types;

public class MemoryImageBlob : MemoryBlob, IImageBlob {
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint Channels { get; set; }
}
