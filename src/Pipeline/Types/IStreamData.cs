namespace Shiron.Lib.Pipeline.Types;

/// <summary>
/// Provides a readable stream over some data source. Used by blob ports for large binary payloads.
/// </summary>
public interface IStreamData : IDisposable {
    /// <summary>Open a new read stream. Callers are responsible for disposing the returned stream.</summary>
    Stream OpenRead();
}
