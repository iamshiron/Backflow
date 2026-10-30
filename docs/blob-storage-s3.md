# S3-compatible blob storage

`Shiron.Backflow.Ext.S3` is a standalone extension implementing the unchanged
`Shiron.Backflow.BlobStorage.IBlobStorage` contract. It depends on Backflow and
AWSSDK.S3; Backflow core, its filesystem storage, JSON caching and other adapters
do not depend on the AWS SDK. It has no Tethys dependency.

## Configuration

```csharp
using Amazon.Runtime;
using Shiron.Backflow.BlobStorage;
using Shiron.Backflow.Ext.S3;

IBlobStorage storage = S3BlobStorage.Create(new S3BlobStorageOptions {
    Name = "generation-cache",
    Bucket = "application-blobs",
    Prefix = "backflow/generation-cache",
    Endpoint = new Uri("https://objects.example.com"),
    Region = "us-east-1",
    MaximumBlobBytes = 256L * 1024 * 1024,
    MaximumConcurrentUploads = 2
}, new EnvironmentVariablesAWSCredentials());

await using (storage) {
    await using var input = File.OpenRead("artifact.bin");
    var id = await storage.StoreAsync(input);
    await using var read = await storage.OpenReadAsync(id);
    await read.CopyToAsync(Stream.Null); // Read the complete body to validate it.
}
```

Supply credentials through server-side secret configuration or an SDK credential
provider. Never commit keys, send them to a browser, include them in an endpoint
URI, or enable SDK credential/request/response body logging. No logging is performed
by this adapter. Factory-created clients use the given endpoint/signing region;
with no endpoint they use AWS regional addressing and the default SDK credential
chain if no credentials are supplied. Custom endpoints require HTTPS unless
`AllowInsecureHttp` explicitly permits HTTP for an isolated local deployment.

For an existing configured `IAmazonS3`, use `new S3BlobStorage(options, client)`.
The injected client remains caller-owned, unless `ownsClient: true` explicitly
transfers ownership. Endpoint, region and addressing options configure the factory;
an injected client's own SDK configuration is authoritative. Configure its request
timeouts/retries/HTTP handler according to deployment needs. Register the instance
wherever an `IBlobStorage`/blob resolver is used; no cache-specific adapter is needed.

The bucket must already exist. The adapter never creates/deletes buckets or changes
bucket policy. Namespace identity is the exact `(bucket, prefix, storage name)`;
keep it stable across restarts. Changing it deliberately isolates previously stored
objects. Prefixes must be nonempty slash-separated ASCII alphanumeric/hyphen/underscore
segments, at most 200 characters, without leading/trailing slashes. Empty/root,
relative traversal, escaped and backslash paths are rejected.

## Publication, bounds and integrity

- Blob IDs are generated lowercase, 32-digit GUIDs. Only this opaque form is accepted;
  callers cannot supply arbitrary keys, URLs or paths. Keys are
  `<prefix>/backflow-v1/<id>`.
- Every input, including nonseekable streams, is incrementally spooled to a random
  exclusive local file while computing its actual length and SHA-256. The input
  remains caller-owned and is consumed from its current position. Declared lengths
  must match exactly. No full-blob memory allocation occurs.
- Limits are explicit per instance: default 256 MiB/blob, two admitted uploads,
  64 KiB copy buffer. Blob size may be zero through five GiB (the single-PUT limit),
  upload concurrency 1–64, buffer 4 KiB–1 MiB, listing page size 1–1000. Spool disk
  usage is bounded by admitted uploads × maximum blob size, plus filesystem overhead.
  ArrayPool buffers may be rounded up to pool bucket sizes; there is also fixed SDK,
  stream and bounded metadata overhead. Multiple instances need a process/deployment
  quota if their combined usage must be bounded.
- Temporary files use `CreateNew`, no sharing, deletion on close, and Unix mode 0600.
  Use a private spool directory, enough disk capacity and appropriate Windows ACLs.
  Cancellation, input errors, size/length mismatches and failed PUTs release admission
  and delete the local spool. A process kill cannot run disposal: deployment cleanup
  of abandoned `backflow-s3-*.tmp` files in its private directory remains necessary.
- The seekable spool is streamed through a **single atomic PUT**, not multipart.
  SHA-256 is sent as the S3 checksum; version, ownership, length, digest and bounded
  application metadata are published with the same object. `If-None-Match: *` prevents
  overwriting an existing key. A collision or unsupported conditional/checksum feature
  fails explicitly; there is no fallback that silently weakens immutability/integrity.
- Tags/content type are preserved in a base64 JSON user-metadata field, capped at
  1,400 encoded bytes to leave room within S3's metadata budget for integrity fields.
  At most 32 tags are allowed, with bounded keys/values. This is cohesive object
  metadata, not a second application registry. The native object MIME type is
  `application/octet-stream`; caller MIME type is retrieved via `GetMetadataAsync`.
- Reads own the SDK `GetObjectResponse` and its HTTP/body stream. Disposing the
  returned stream (sync or async) disposes that response exactly once. Cancellation,
  read failures and detected corruption also close it. Reads are nonseekable and
  incrementally verify the digest when the declared length is reached, reject
  premature EOF, and reject excess bytes if observed. Empty-buffer reads are not EOF.
  Read the complete body; disposing an intentionally partial read cannot prove the
  unread bytes. Bytes preceding the final digest check may already have been consumed.
  Metadata/exists operations validate headers, not unread object-body hashes.

Single-PUT publication avoids multipart orphan uploads, but network cancellation or
lost acknowledgments can leave an uncertain committed object whose ID was not
returned. The adapter does not claim rollback of an ambiguous remote commit. Scoped
clear or a deployment retention process can remove orphaned owned objects. Checksums
detect corruption; they are not a MAC against a principal able to replace both body
and metadata.

## Ownership, delete and clear

Ownership requires the reserved format marker plus a deterministic hash of the
namespace identity. `RemoveAsync` checks ownership with HEAD before issuing an
ETag-conditional delete. `ClearAsync` requests only its exact prefix with bounded
`ListObjectsV2` pages, validates every returned key locally, checks object ownership,
and deletes individually with ETag preconditions. It neither deletes foreign objects
with UUID-shaped keys inside that prefix nor objects outside it. Missing/unchanged
pagination continuations fail instead of silently returning a partial success.

These markers are operational ownership, **not authentication**. Configure a dedicated
namespace and least-privilege bucket policy. A malicious writer with permission to
forge markers or replace an object using the same ETag can defeat metadata-based
ownership; no portable S3 API provides a metadata compare-and-delete transaction.
Concurrent external overwrites are unsupported. Conditional failure is propagated,
never bypassed. Clear is not a bucket transaction or a distributed barrier: concurrent
stores can survive it, and cancellation/errors can leave a partially cleared scope.
Quiesce writers when an empty scope is required.

Deletes operate on current objects, not historical versions. Use an unversioned
cache bucket, or separately configure version-retention/lifecycle rules; clear does
not promise physical erasure of historical object versions. Dispose active read
streams and finish requests before disposing a storage instance/owned SDK client.

Missing owned IDs produce false from exists/remove, null metadata, and
`FileNotFoundException` on open. An object belonging to another namespace is likewise
not this adapter's blob. Only recognized object 404s are treated as missing; a bucket HEAD
confirms the bucket still exists so a missing bucket is not silently interpreted as
an empty store. This requires bucket HEAD/ListBucket permission. Permission errors,
service/network failures, malformed owned metadata and conditional conflicts propagate
honestly. SDK exceptions can contain service endpoint/key diagnostics; application
logging policy must still avoid logging credentials and payloads.

## Tests and measured compatibility

Unit tests inject an SDK-client boundary and cover request shape, streaming/nonseekable
inputs, admission/size/metadata limits, declared-length mismatch, cancellation at both
spooling and upload, cleanup, identifier/prefix isolation, pagination, permission/error
handling, corruption/short/excess bodies, zero-length objects and HTTP-body ownership.

The opt-in RustFS test is an integration test because actual checksum/conditional
request behavior, user-metadata encoding and pagination semantics cannot be established
by an in-memory SDK substitute. It creates a unique bucket and deletes only that
bucket's test objects. Run only against a **new disposable task-owned RustFS container**,
never existing user storage. Set these through untracked local secret configuration:

- `BACKFLOW_RUSTFS_TEST_ENDPOINT`
- `BACKFLOW_RUSTFS_TEST_ACCESS_KEY`
- `BACKFLOW_RUSTFS_TEST_SECRET_KEY`

```sh
dotnet test tests/Backflow.Tests.Ext.S3/Shiron.Backflow.Tests.Ext.S3.csproj --configuration Release
```

Without those settings the RustFS fact is explicitly skipped; unit tests still run.
Development validation on 2026-10-30 used local RustFS **1.0.3**, image revision
`331fb896285a7686f927baf8d9d4dc2d3d3a10a9`, in a uniquely named Podman container,
random loopback port, disposable tmpfs data/logs, no existing volumes or buckets.
The real lifecycle test passed: nonseekable and multi-buffer 2 MiB uploads, empty
objects, checksum/header round-trips, reopen, corruption detection, conditional
remove, two-key pagination, scoped clear preserving foreign/sibling objects, and
wrong-credential 403 behavior. This establishes local RustFS support for the exercised
production adapter. AWS hosted S3 and other compatible implementations were **not**
live-tested; they must support these documented S3 APIs/conditions. No paid APIs
were called. No live-provider success is simulated by production code.
