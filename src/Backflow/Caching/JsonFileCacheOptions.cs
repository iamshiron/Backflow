namespace Shiron.Backflow.Caching;

/// <summary>Bounds and trusted deserialization types for a JSON cache.</summary>
public sealed class JsonFileCacheOptions {
    /// <summary>Maximum entries in each pending buffer and in the persisted store.</summary>
    public int MaximumEntries { get; init; } = 1024;
    /// <summary>Maximum lifetime measured from the entry's creation time.</summary>
    public TimeSpan MaximumAge { get; init; } = TimeSpan.FromDays(7);
    /// <summary>Maximum serialized size of an individual entry.</summary>
    public int MaximumEntryBytes { get; init; } = 1024 * 1024;
    /// <summary>Maximum aggregate serialized payload bytes retained by the private pending buffer.</summary>
    public long MaximumPendingBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>Maximum size read from the metadata file.</summary>
    public long MaximumStoreBytes { get; init; } = 64 * 1024 * 1024;
    /// <summary>Maximum wait for another process's storage operation.</summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Clock used for retention, including deterministic tests.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    /// <summary>Application-owned types permitted when reopening persisted entries. Never resolved from file type names.</summary>
    public IReadOnlyCollection<Type> AllowedTypes { get; init; } = [];
    internal Action<string, string>? BeforePublish { get; init; }
}
