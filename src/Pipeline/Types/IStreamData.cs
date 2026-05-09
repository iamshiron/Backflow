namespace Shiron.Lib.Pipeline.Types;

public interface IStreamData : IDisposable {
    Stream OpenRead();
}
