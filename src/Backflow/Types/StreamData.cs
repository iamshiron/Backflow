using Shiron.Backflow.Types;

namespace Shiron.Backflow.Types;

/// <summary>
/// <see cref="IStreamData"/> backed by a stream factory. Each <see cref="OpenRead"/> call creates a fresh stream.
/// </summary>
public class StreamData(Func<Stream> streamFactory) : IStreamData {
    public Stream OpenRead() {
        return streamFactory();
    }

    public void Dispose() { }
}
