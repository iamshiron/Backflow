using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Types;

/// <summary>
/// Minimal <see cref="IBlob"/> wrapper over any <see cref="IStreamData"/>. No metadata attached.
/// </summary>
public readonly struct RawBlob(IStreamData storage) : IBlob {
    public IStreamData Storage { get; } = storage;
}
