# Durable JSON cache and filesystem blobs

Backflow's standalone `ICache` and `IBlobStorage` contracts do not depend on an
application database or hosting framework. This implementation uses JSON metadata
and filesystem blobs. An S3/RustFS adapter can implement the same blob contract
without changing the cache. PostgreSQL and S3 are not required by this backend.

## Configuration

```csharp
using Shiron.Backflow.Caching;
using Shiron.Backflow.BlobStorage;

using var cache = new JsonFileCache("/data/cache/nodes.json", options: new JsonFileCacheOptions {
    MaximumEntries = 1024,
    MaximumEntryBytes = 1024 * 1024,
    MaximumPendingBytes = 16 * 1024 * 1024,
    MaximumStoreBytes = 64 * 1024 * 1024,
    MaximumAge = TimeSpan.FromDays(7),
    AllowedTypes = [typeof(MyResult)]
});
using var blobs = new FileSystemBlobStorage("disk", "/data/blobs", new FileSystemBlobStorageOptions {
    MaximumBlobs = 10000,
    MaximumBlobBytes = 1024L * 1024 * 1024,
    MaximumTotalBytes = 10L * 1024 * 1024 * 1024,
    Retention = TimeSpan.FromDays(7)
});

// After successful cache writes, at an application-defined durability boundary:
await cache.FlushAsync();
```

`MyResult` is an application DTO, not a required library type. Serialization
converters can be supplied through `CacheTypeAdapterRegistry`. Configure singleton
instances once; options and converters must not be mutated afterward. Both stores
support concurrent calls and multiple cooperating instances/processes.
`CacheKeyFactory` can also be shared across concurrent executions with separate
contexts: hashing buffers are local, serializer options support concurrent reads,
and open-generic adapter lookup uses a concurrent dictionary. Application converters
and identity callbacks must themselves be thread-safe; configure the adapter registry
before constructing a cache/factory, never concurrently with registration.

## Cache consistency and durability

* `SetAsync` serializes a snapshot into a **private, bounded pending buffer**.
  Reads return newly deserialized values, not references to mutable cached objects.
  Pending writes are not visible to other instances until flush. Dispose deliberately
  discards pending writes, preserving the existing public contract; it never silently
  performs a blocking, fallible flush.
* Flush acquires a persistent lock file, reloads current committed metadata, merges
  pending entries, removes expired/excess entries, writes a unique sibling temporary
  file, flushes it to disk, and atomically renames it over the destination. Pending
  entries are cleared **only after successful publication**. Failed flushes can be retried.
* Retention uses entry creation time, not sliding access time. Newest entries win
  capacity eviction with an ordinal hash tie-breaker. Entry count and aggregate pending
  payload bytes bound memory; entry and store byte limits bound serialization and
  persisted metadata. File-size eviction may discard an entry larger than the whole
  configured store. Existing oversized stores are rejected rather than read unboundedly.
  Expiration/cleanup runs on access/flush, not on an implicit background timer.
  Entry values stream directly into a bounded UTF-8 destination: there is no
  intermediate port-value JSON DOM or unbounded payload string. The smaller of the
  entry and pending budgets is enforced during emission. Rejected writes leave both
  prior entries and the trusted-type registry unchanged. Store rewrites likewise
  serialize into a bounded destination and evict/retry on budget exhaustion.
  Writer scratch reservations are capped at six times the byte budget plus 4096
  bytes for conservative UTF-8 token/growth requests; committed payloads cannot exceed
  the budget. Default string and raw-JSON converters reject oversized scalar inputs
  before framework escaping/unescaping allocations (raw JSON must fit before whitespace
  normalization). Application converters/getters remain trusted code: allocations
  they perform themselves cannot be sandboxed by an output-buffer limit.
* Locking covers each committed read/modify/write operation across instances and
  processes. Lock files remain in place: deleting them could create two independent
  lock identities. Timeout raises an I/O error; it does not return a fake cache miss.
  Private pending writes are not transactions spanning instances: the last flush for
  the same key wins. Clear/remove do not cancel another instance's pending writes;
  a later flush is a new write. Coordinate writers if global invalidation is required.
* Checksummed entry payloads include the complete key and timestamp. A mismatched key,
  corrupt payload, unsupported type, or failed value deserialization invalidates that
  entry. A malformed/legacy metadata file is moved to one bounded `.corrupt` quarantine
  path and treated as a miss. Unpublished temporary metadata is discarded on recovery.
  I/O failures propagate instead of being disguised as missing data.
* Persisted type names are looked up in a trusted allowlist, **never** passed to
  `Type.GetType`. Primitive/BCL scalar types, their arrays, and Backflow blob cache
  contracts are built in. Other runtime types observed in trusted `SetAsync` calls are
  permitted in that instance. On reopening, register application types explicitly in
  `AllowedTypes`; otherwise entries requiring them are invalidated. There is no
  fallback to an incorrectly typed `JsonElement`.

## Deterministic identities

Keys include a versioned structural encoding of node assembly-qualified identity,
assembly version, input names, declared/runtime types, presence, indexed supplied
masks, and canonical JSON values. Object/dictionary properties use ordinal sorting;
arrays retain order. Binary Backflow inputs hash their actual content, and typed
blobs include their metadata. SHA-256 is used for all hashes; delimiter-containing
component values cannot alias other component boundaries. Even a custom key whose
hash collides with another key is checked against the full stored key before a hit.

Nodes and custom contexts implement `ICacheIdentityProvider` when settings/options,
external data versions, or other semantic context affect their results:

```csharp
public object? CacheIdentity => Settings;
```

The value must be a deterministic immutable JSON-serializable snapshot. Do not
include execution counters or ephemeral object identity. Never cache a node with
undeclared semantic dependencies. `CacheKeyFactoryOptions.IdentitySelector` supplies
additional node/context identity where implementing the interface is impractical;
`Version` explicitly invalidates prior application algorithms even if assembly
version remains fixed. Converter configuration changes that alter semantics without
changing serialized values must increment this version. Converters must be
deterministic; JSON serialization intentionally rejects unsupported/cyclic values.
JSON numeric spellings are not normalized across different custom converters.

## Blob integrity, ownership and cleanup

Each Store call assigns an independent random ID, streams into a temporary file
with bounded copying, hashes bytes, flushes them, publishes the blob, then atomically
publishes registry metadata. No bytes are returned before verifying stored length
and SHA-256. Missing/corrupt blob data invalidates its registration and raises a
missing-data/integrity error. Metadata's caller-supplied ContentLength is preserved
for compatibility; integrity and capacity always use the independently measured length.
The registry is reloaded under the process lock, so instances cannot overwrite each
other's entries with stale in-memory snapshots. A corrupt/legacy registry fails closed
with an explicit recovery error rather than silently discarding ownership metadata.

There is deliberately **no physical deduplication**: the existing blob interface has
no ownership lease/refcount contract, so sharing one deletable ID would be unsafe.
Removing one identical upload cannot delete another upload. Remove/clear reject
actively open readers. Cache expiration never automatically deletes blobs: a blob
may still be referenced by another cache entry or by the application.

Admission rejects writes at configured count/byte capacity rather than evicting
active blobs. For bounded lifetime cleanup, schedule
`CollectGarbageAsync(completeLiveBlobIds)` after reconciling all reference owners.
It deletes only expired IDs absent from that complete live set, plus expired
unpublished temporary files and orphaned data files. The application must prevent
new references to collectible IDs during this operation. Explicit Remove/Clear
similarly require ownership authorization by the caller. Clear only touches known
registered blob paths, never recursively deletes unrelated directories.

Crash after blob publication but before registry commit can leave an unreferenced
file; it cannot create a valid partial hit. Explicit garbage collection recovers it.
Registry removal commits before physical deletion, so interruption during cleanup
can also leave harmless unregistered data for the next collection.

## Filesystem and performance boundaries

Use an application-owned directory on a **local filesystem** with reliable atomic
same-directory rename and OS file sharing. Paths and existing ancestors are checked
for symlinks/reparse points, and blob IDs cannot contain traversal/separator/escape
syntax. This is not a sandbox against a hostile process that can concurrently change
directory entries; filesystem permissions must prevent that race. Hard links and
out-of-band modifications are outside the ownership contract. Do not delete or replace
lock files while stores are in use. Network/distributed filesystems are not supported.

Files are flushed to disk before publication. On Linux, containing directories are
also fsynced after rename/publication/deletion. Other platforms get atomic process-crash
publication, but directory power-loss persistence is not guaranteed by this adapter.
Hardware/filesystem durability still depends on correct fsync/rename behavior. A
failure after rename but before directory sync has an ambiguous commit outcome:
retry is safe, and pending data is retained. No claim of a distributed transaction
between node metadata and blobs is made.

The JSON backend prioritizes correctness for modest caches: flush rewrites metadata,
committed reads deserialize it, and integrity verification rereads whole blobs.
Serialization, canonical sorting, cryptographic hashing and materialization allocate;
this is **not** a zero-allocation hot-path cache. Buffer pooling limits streaming
copy allocations. Use another `ICache`/`IBlobStorage` implementation when workload
size or distributed coordination exceeds these limits. The existing ICache contract
has no cancellation token; blob operations do. Dispose is idempotent and operations
after disposal fail predictably.

Validation includes unit tests for identities and real-filesystem integration tests
for atomic failure injection, stale temporary files, corrupt metadata/data, instance
and OS process locking, capacity/retention, mutable snapshots, active-reader deletion,
and symlink boundaries. These require OS filesystem semantics, so memory-only doubles
cannot establish their guarantees. No proprietary APIs or AI inference are involved.
