namespace Shiron.Lib.Pipeline.Types;

public readonly struct RawBlob(IStreamData storage) : IBlob {
    public IStreamData Storage { get; } = storage;
}
