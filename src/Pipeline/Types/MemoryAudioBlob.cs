namespace Shiron.Lib.Pipeline.Types;

public class MemoryAudioBlob : MemoryBlob, IAudioBlob {
    public uint SampleRate { get; set; }
    public uint Channels { get; set; }
}
