using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Types;

public interface IBlob {
    IStreamData Storage { get; }
}

public interface IBlob<out TMeta, out TStorage> : IBlob where TStorage : IStreamData {
    TMeta Meta { get; }
    new TStorage Storage { get; }
}

public class Blob<TMeta, TStorage>(TMeta meta, TStorage storage) : IBlob<TMeta, TStorage> where TStorage : IStreamData {
    public TMeta Meta { get; } = meta;
    public TStorage Storage { get; } = storage;
    IStreamData IBlob.Storage => Storage;
}
