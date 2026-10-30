namespace Shiron.Backflow.BlobStorage;

/// <summary>Admission limits and explicit garbage collection policy for filesystem blobs.</summary>
public sealed class FileSystemBlobStorageOptions {
    /// <summary>Maximum size of one blob; enforced while copying the input stream.</summary>
    public long MaximumBlobBytes { get; init; } = 1024L * 1024 * 1024;
    /// <summary>Maximum total registered bytes. At capacity new writes fail, rather than deleting live blobs.</summary>
    public long MaximumTotalBytes { get; init; } = 10L * 1024 * 1024 * 1024;
    /// <summary>Maximum number of registered blobs.</summary>
    public int MaximumBlobs { get; init; } = 10000;
    /// <summary>Maximum registry size read or written.</summary>
    public long MaximumRegistryBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>Minimum age for explicit garbage collection of blobs not in the caller's live set.</summary>
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(7);
    /// <summary>Maximum wait for another process's storage operation.</summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Clock for creation timestamps and garbage collection.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    internal Action<string, string>? BeforePublish { get; init; }
}
