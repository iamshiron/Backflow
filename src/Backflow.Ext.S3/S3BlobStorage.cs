using System.Buffers;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Shiron.Backflow.BlobStorage;

namespace Shiron.Backflow.Ext.S3;

/// <summary>Immutable, bounded, single-object S3 blob storage with scoped ownership and streaming SHA-256 verification.</summary>
public sealed class S3BlobStorage : IBlobStorage {
    private const string FormatKey = "backflow-format";
    private const string OwnerKey = "backflow-owner";
    private const string LengthKey = "backflow-length";
    private const string HashKey = "backflow-sha256";
    private const string MetadataKey = "backflow-metadata";
    private const string FormatVersion = "1";
    private const int MaximumEncodedMetadataBytes = 1400;
    private readonly S3BlobStorageOptions _options;
    private readonly IAmazonS3 _client;
    private readonly bool _ownsClient;
    private readonly string _keyPrefix;
    private readonly string _owner;
    private readonly SemaphoreSlim _uploads;
    private int _disposed;

    /// <summary>Creates storage using an injected S3 client; the caller retains client ownership unless explicitly transferred.</summary>
    public S3BlobStorage(S3BlobStorageOptions options, IAmazonS3 client, bool ownsClient = false) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        options.Validate();
        _options = options;
        _client = client;
        _ownsClient = ownsClient;
        _keyPrefix = options.Prefix + "/backflow-v1/";
        _owner = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Bucket + "\n" + options.Prefix + "\n" + options.Name)));
        _uploads = new SemaphoreSlim(options.MaximumConcurrentUploads);
    }

    /// <summary>Creates an owned SDK client from server configuration and optional credentials, otherwise using the SDK credential chain.</summary>
    public static S3BlobStorage Create(S3BlobStorageOptions options, AWSCredentials? credentials = null) {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };
        if (options.Endpoint is { } endpoint) {
            config.ServiceURL = endpoint.AbsoluteUri;
            config.AuthenticationRegion = options.Region;
        } else config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        return new S3BlobStorage(options, credentials is null ? new AmazonS3Client(config) : new AmazonS3Client(credentials, config), true);
    }

    /// <inheritdoc/>
    public string Name => _options.Name;

    /// <inheritdoc/>
    public async ValueTask<string> StoreAsync(Stream data, BlobMetadata? metadata = null, CancellationToken ct = default) {
        CheckDisposed();
        ArgumentNullException.ThrowIfNull(data);
        if (!data.CanRead) throw new ArgumentException("Input must be readable.", nameof(data));
        if (metadata?.ContentLength is < 0 || metadata?.ContentLength > _options.MaximumBlobBytes)
            throw new ArgumentOutOfRangeException(nameof(metadata));
        var encodedMetadata = EncodeMetadata(metadata);
        await _uploads.WaitAsync(ct).ConfigureAwait(false);
        try {
            CheckDisposed();
            Directory.CreateDirectory(_options.TemporaryDirectory);
            var path = Path.Combine(_options.TemporaryDirectory, "backflow-s3-" + Guid.NewGuid().ToString("N") + ".tmp");
            var fileOptions = new FileStreamOptions {
                Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
                BufferSize = _options.BufferBytes, Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
            };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using var spool = new FileStream(path, fileOptions);
            var buffer = ArrayPool<byte>.Shared.Rent(_options.BufferBytes);
            long length = 0;
            byte[] digest;
            try {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                while (true) {
                    ct.ThrowIfCancellationRequested();
                    var read = await data.ReadAsync(buffer.AsMemory(0, _options.BufferBytes), ct).ConfigureAwait(false);
                    if (read == 0) break;
                    if (read > _options.MaximumBlobBytes - length) throw new InvalidDataException("Blob exceeds the configured size limit.");
                    length += read;
                    hash.AppendData(buffer, 0, read);
                    await spool.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }
                if (metadata?.ContentLength is { } expected && expected != length)
                    throw new InvalidDataException("Declared content length does not match the input stream.");
                digest = hash.GetHashAndReset();
            } finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
            await spool.FlushAsync(ct).ConfigureAwait(false);
            spool.Position = 0;
            var id = Guid.NewGuid().ToString("N");
            var request = new PutObjectRequest {
                BucketName = _options.Bucket, Key = Key(id), InputStream = spool,
                AutoCloseStream = false, AutoResetStreamPosition = true, UseChunkEncoding = false,
                IfNoneMatch = "*", ChecksumSHA256 = Convert.ToBase64String(digest),
                ContentType = "application/octet-stream"
            };
            request.Metadata[FormatKey] = FormatVersion;
            request.Metadata[OwnerKey] = _owner;
            request.Metadata[LengthKey] = length.ToString(CultureInfo.InvariantCulture);
            request.Metadata[HashKey] = Convert.ToHexString(digest);
            request.Metadata[MetadataKey] = encodedMetadata;
            await _client.PutObjectAsync(request, ct).ConfigureAwait(false);
            return id;
        } finally { _uploads.Release(); }
    }

    /// <inheritdoc/>
    public async ValueTask<Stream> OpenReadAsync(string blobId, CancellationToken ct = default) {
        CheckDisposed();
        var key = Key(blobId);
        GetObjectResponse response;
        try { response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key }, ct).ConfigureAwait(false); } catch (AmazonS3Exception error) when (IsMissingObject(error)) {
            await ConfirmBucketAsync(ct).ConfigureAwait(false);
            throw new FileNotFoundException("Blob was not found.");
        }
        try {
            if (!IsOwned(response.Metadata)) throw new FileNotFoundException("Blob is not owned by this storage namespace.");
            var header = ReadHeader(response.Metadata, response.ContentLength);
            return new S3VerifiedReadStream(response, header.Length, header.Digest);
        } catch { response.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public async ValueTask<BlobMetadata?> GetMetadataAsync(string blobId, CancellationToken ct = default) {
        CheckDisposed();
        var response = await HeadAsync(Key(blobId), ct).ConfigureAwait(false);
        return response is null || !IsOwned(response.Metadata) ? null : ReadHeader(response.Metadata, response.ContentLength).Metadata;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ExistsAsync(string blobId, CancellationToken ct = default) {
        CheckDisposed();
        var response = await HeadAsync(Key(blobId), ct).ConfigureAwait(false);
        if (response is null || !IsOwned(response.Metadata)) return false;
        ReadHeader(response.Metadata, response.ContentLength);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(string blobId, CancellationToken ct = default) {
        CheckDisposed();
        return await RemoveKeyAsync(Key(blobId), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ClearAsync(CancellationToken ct = default) {
        CheckDisposed();
        string? token = null;
        do {
            var page = await _client.ListObjectsV2Async(new ListObjectsV2Request {
                BucketName = _options.Bucket, Prefix = _keyPrefix, MaxKeys = _options.ListPageSize, ContinuationToken = token
            }, ct).ConfigureAwait(false);
            foreach (var item in page.S3Objects ?? []) {
                ct.ThrowIfCancellationRequested();
                if (item.Key is { } key && key.StartsWith(_keyPrefix, StringComparison.Ordinal)
                    && IsSafeId(key[_keyPrefix.Length..])) await RemoveKeyAsync(key, ct).ConfigureAwait(false);
            }
            if (page.IsTruncated != true) break;
            if (string.IsNullOrEmpty(page.NextContinuationToken) || page.NextContinuationToken == token)
                throw new InvalidDataException("S3 returned an invalid pagination continuation.");
            token = page.NextContinuationToken;
        } while (true);
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _ownsClient) _client.Dispose();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }

    private async Task<bool> RemoveKeyAsync(string key, CancellationToken ct) {
        var head = await HeadAsync(key, ct).ConfigureAwait(false);
        if (head is null || !IsOwned(head.Metadata)) return false;
        if (string.IsNullOrEmpty(head.ETag)) throw new InvalidDataException("Owned object has no conditional deletion identity.");
        try {
            await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _options.Bucket, Key = key, IfMatch = head.ETag }, ct).ConfigureAwait(false);
            return true;
        } catch (AmazonS3Exception error) when (IsMissingObject(error)) {
            await ConfirmBucketAsync(ct).ConfigureAwait(false);
            return false;
        }
    }

    private async Task<GetObjectMetadataResponse?> HeadAsync(string key, CancellationToken ct) {
        try { return await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = _options.Bucket, Key = key }, ct).ConfigureAwait(false); } catch (AmazonS3Exception error) when (IsMissingObject(error)) {
            await ConfirmBucketAsync(ct).ConfigureAwait(false);
            return null;
        }
    }

    private async Task ConfirmBucketAsync(CancellationToken ct)
        => await _client.HeadBucketAsync(new HeadBucketRequest { BucketName = _options.Bucket }, ct).ConfigureAwait(false);

    private static bool IsMissingObject(AmazonS3Exception error)
        => error.StatusCode == HttpStatusCode.NotFound
            && (string.IsNullOrEmpty(error.ErrorCode) || error.ErrorCode is "NotFound" or "NoSuchKey");

    private bool IsOwned(MetadataCollection metadata) => metadata[FormatKey] == FormatVersion && metadata[OwnerKey] == _owner;

    private (long Length, byte[] Digest, BlobMetadata Metadata) ReadHeader(MetadataCollection metadata, long contentLength) {
        if (!long.TryParse(metadata[LengthKey], NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            || length < 0 || length > _options.MaximumBlobBytes || length != contentLength)
            throw new InvalidDataException("Owned object length metadata is invalid.");
        var hex = metadata[HashKey];
        if (hex is null || hex.Length != 64 || !hex.All(char.IsAsciiHexDigit))
            throw new InvalidDataException("Owned object digest metadata is invalid.");
        try {
            var encoded = metadata[MetadataKey];
            if (encoded is null || encoded.Length > MaximumEncodedMetadataBytes) throw new InvalidDataException("Owned object metadata exceeds its limit.");
            var decoded = JsonSerializer.Deserialize<BlobMetadata>(Convert.FromBase64String(encoded))
                ?? throw new InvalidDataException("Owned object metadata is missing.");
            _ = EncodeMetadata(decoded);
            return (length, Convert.FromHexString(hex), decoded with { ContentLength = length });
        } catch (Exception error) when (error is FormatException or JsonException or ArgumentException) {
            throw new InvalidDataException("Owned object metadata is invalid.", error);
        }
    }

    private static string EncodeMetadata(BlobMetadata? metadata) {
        if (metadata?.Tags is null && metadata is not null) throw new ArgumentException("Metadata tags cannot be null.", nameof(metadata));
        var value = metadata ?? new BlobMetadata();
        if (value.ContentType is { } contentType && (contentType.Length > 200 || contentType.Any(char.IsControl)))
            throw new ArgumentException("Invalid content type.", nameof(metadata));
        if (value.Tags.Count > 32 || value.Tags.Any(pair => pair.Key is null || pair.Value is null
            || pair.Key.Length > 200 || pair.Value.Length > 500)) throw new ArgumentException("Metadata tags exceed supported bounds.", nameof(metadata));
        var encoded = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value with { ContentLength = null }));
        if (encoded.Length > MaximumEncodedMetadataBytes) throw new ArgumentException("Encoded metadata exceeds the S3 header budget.", nameof(metadata));
        return encoded;
    }

    private string Key(string id) {
        if (!IsSafeId(id)) throw new ArgumentException("Expected a lowercase 32-digit immutable blob identifier.", nameof(id));
        return _keyPrefix + id;
    }

    private static bool IsSafeId(string? id) => id is { Length: 32 } && id.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
