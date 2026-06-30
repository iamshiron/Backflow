using Shiron.Backflow.Types;

namespace Shiron.Backflow.Types;

/// <summary>
/// Minimal <see cref="IBlob"/> wrapper over any <see cref="IStreamData"/>. No metadata attached.
/// </summary>
public readonly struct RawBlob(IStreamData storage) : IBlob {
    public IStreamData Storage { get; } = storage;
}
