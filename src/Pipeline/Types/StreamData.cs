using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Types;

public class StreamData(Func<Stream> streamFactory) : IStreamData {
    public Stream OpenRead() {
        return streamFactory();
    }

    public void Dispose() { }
}
