using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Types;

/// <summary>
/// Untyped blob — wraps an <see cref="IStreamData"/> storage backend. Used for binary file-like payloads
/// that are too large for inline port values.
/// </summary>
public interface IBlob {
    /// <summary>The underlying storage providing the read stream.</summary>
    IStreamData Storage { get; }
}

/// <summary>
/// Typed blob with metadata and a typed storage backend.
/// </summary>
/// <typeparam name="TMeta">Metadata type (e.g., filename, content-type).</typeparam>
/// <typeparam name="TStorage">Storage backend type.</typeparam>
public interface IBlob<out TMeta, out TStorage> : IBlob where TStorage : IStreamData {
    /// <summary>Metadata associated with this blob.</summary>
    TMeta Meta { get; }
    /// <summary>Typed storage backend.</summary>
    new TStorage Storage { get; }
}

/// <summary>Concrete <see cref="IBlob{TMeta,TStorage}"/> implementation.</summary>
public class Blob<TMeta, TStorage>(TMeta meta, TStorage storage) : IBlob<TMeta, TStorage> where TStorage : IStreamData {
    public TMeta Meta { get; } = meta;
    public TStorage Storage { get; } = storage;
    IStreamData IBlob.Storage => Storage;
}
