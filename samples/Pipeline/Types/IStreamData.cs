namespace Shiron.Lib.Samples.Pipeline.Types;

public interface IStreamData : IDisposable {
    Stream OpenRead();
}
