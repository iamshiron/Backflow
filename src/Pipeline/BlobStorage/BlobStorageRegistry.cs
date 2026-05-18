using System.Collections.Concurrent;

namespace Shiron.Lib.Pipeline.BlobStorage;

public class BlobStorageRegistry : IBlobStorageResolver {
    private readonly ConcurrentDictionary<string, IBlobStorage> _storages = new();
    private readonly List<IBlobStorage> _registrationOrder = [];
    private readonly Lock _lock = new();

    protected void Register(IBlobStorage storage) {
        ArgumentNullException.ThrowIfNull(storage);
        if (string.IsNullOrWhiteSpace(storage.Name))
            throw new ArgumentException("Storage must have a non-empty Name.", nameof(storage));

        _storages[storage.Name] = storage;
        lock (_lock) {
            _registrationOrder.Add(storage);
        }
    }

    public virtual IBlobStorage Resolve(BlobMetadata? metadata) {
        lock (_lock) {
            if (_registrationOrder.Count == 0)
                throw new InvalidOperationException("No blob storages registered.");
            return _registrationOrder[0];
        }
    }

    public IBlobStorage ResolveByName(string storageName) {
        return _storages.TryGetValue(storageName, out var storage)
            ? storage
            : throw new KeyNotFoundException($"No blob storage registered with name '{storageName}'.");
    }

    public async ValueTask DisposeAsync() {
        foreach (var storage in _storages.Values) {
            if (storage is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                storage.Dispose();
        }
        _storages.Clear();
        lock (_lock) {
            _registrationOrder.Clear();
        }
    }

    public void Dispose() {
        foreach (var storage in _storages.Values)
            storage.Dispose();
        _storages.Clear();
        lock (_lock) {
            _registrationOrder.Clear();
        }
    }
}
