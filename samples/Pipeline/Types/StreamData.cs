namespace Shiron.Lib.Samples.Pipeline.Types;

public class StreamData(Func<Stream> streamFactory) : IStreamData {
    public Stream OpenRead() {
        return streamFactory();
    }

    public void Dispose() { }
}
