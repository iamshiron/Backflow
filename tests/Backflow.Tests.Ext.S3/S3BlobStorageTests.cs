using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Shiron.Backflow.BlobStorage;
using Shiron.Backflow.Ext.S3;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.S3;

public sealed class S3BlobStorageTests {
    [Fact]
    public async Task StoreAsync_NonseekableInput_PublishesBodyAndIntegrityMetadataTogether() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        var bytes = Encoding.UTF8.GetBytes("streamed blob");
        PutObjectRequest? captured = null;
        client.Put = async (request, ct) => {
            captured = request;
            Assert.True(request.InputStream.CanSeek);
            Assert.Equal(bytes.Length, request.InputStream.Length);
            using var output = new MemoryStream();
            await request.InputStream.CopyToAsync(output, ct);
            Assert.Equal(bytes, output.ToArray());
            Assert.Equal("*", request.IfNoneMatch);
            Assert.False(request.AutoCloseStream);
            Assert.Equal(Convert.ToBase64String(SHA256.HashData(bytes)), request.ChecksumSHA256);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), request.Metadata["backflow-sha256"]);
            Assert.Equal(bytes.Length.ToString(), request.Metadata["backflow-length"]);
            Assert.Single(Directory.GetFiles(directory.Path));
            if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Assert.Single(Directory.GetFiles(directory.Path))));
            return new PutObjectResponse();
        };
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        using var input = new NonseekableStream(bytes);
        var id = await storage.StoreAsync(input, new BlobMetadata { ContentLength = bytes.Length, ContentType = "text/plain" });
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.Equal("test/cache/backflow-v1/" + id, captured!.Key);
        Assert.True(input.CanRead);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Theory]
    [InlineData(5, 6)]
    [InlineData(20, 5)]
    public async Task StoreAsync_LengthMismatchOrLimitFailure_NeverPublishesAndCleansSpool(int maximum, int declared) {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(directory.Path) with { MaximumBlobBytes = maximum }, client);
        using var input = new NonseekableStream(new byte[6]);
        if (declared > maximum) await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => storage.StoreAsync(input,
            new BlobMetadata { ContentLength = declared }).AsTask());
        else await Assert.ThrowsAsync<InvalidDataException>(() => storage.StoreAsync(input,
            new BlobMetadata { ContentLength = declared }).AsTask());
        Assert.Equal(0, client.PutCalls);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task StoreAsync_ActualLimitExceededWithoutDeclaredLength_CleansSpool() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(directory.Path) with { MaximumBlobBytes = 5 }, client);
        using var input = new NonseekableStream(new byte[6]);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.StoreAsync(input).AsTask());
        Assert.Empty(Directory.GetFiles(directory.Path));
        Assert.Equal(0, client.PutCalls);
    }

    [Fact]
    public async Task StoreAsync_CancelledUploadAndInputFailure_CleansSpoolAndReleasesAdmission() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        using var cancellation = new CancellationTokenSource();
        client.Put = async (_, ct) => { cancellation.Cancel(); await Task.Delay(Timeout.Infinite, ct); return new PutObjectResponse(); };
        await using var storage = new S3BlobStorage(Options(directory.Path) with { MaximumConcurrentUploads = 1 }, client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(new MemoryStream([1, 2]), ct: cancellation.Token).AsTask());
        Assert.Empty(Directory.GetFiles(directory.Path));
        client.Put = (_, _) => Task.FromException<PutObjectResponse>(new IOException("Injected upload failure"));
        await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync(new MemoryStream([3])).AsTask());
        Assert.Empty(Directory.GetFiles(directory.Path));
        using var brokenInput = new BrokenInputStream();
        await Assert.ThrowsAsync<IOException>(() => storage.StoreAsync(brokenInput).AsTask());
        Assert.Empty(Directory.GetFiles(directory.Path));
        client.Put = (_, _) => Task.FromResult(new PutObjectResponse());
        Assert.NotNull(await storage.StoreAsync(new MemoryStream([4])));
    }

    [Fact]
    public async Task StoreAsync_AdmissionLimit_CancelledWaiterCreatesNoExtraSpool() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Put = async (_, ct) => { entered.SetResult(); await release.Task.WaitAsync(ct); return new PutObjectResponse(); };
        await using var storage = new S3BlobStorage(Options(directory.Path) with { MaximumConcurrentUploads = 1 }, client);
        var first = storage.StoreAsync(new MemoryStream([1])).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var cancelled = new CancellationTokenSource();
        var second = storage.StoreAsync(new MemoryStream([2]), ct: cancelled.Token).AsTask();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Single(Directory.GetFiles(directory.Path));
        Assert.Equal(1, client.PutCalls);
        release.SetResult();
        await first;
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task StoreAsync_CancellationDuringSpooling_CleansFileWithoutPublishing() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        using var cancellation = new CancellationTokenSource();
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        using var input = new CancellingInputStream(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(input, ct: cancellation.Token).AsTask());
        Assert.Empty(Directory.GetFiles(directory.Path));
        Assert.Equal(0, client.PutCalls);
    }

    [Fact]
    public async Task StoreAsync_UnboundedMetadata_IsRejectedBeforeAnyRequestOrSpool() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        var tags = Enumerable.Range(0, 32).ToDictionary(index => index.ToString(), _ => new string('x', 500));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(new MemoryStream(), new BlobMetadata { Tags = tags }).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(new MemoryStream(), new BlobMetadata { ContentType = "text/plain\r\ninjected" }).AsTask());
        Assert.Empty(Directory.GetFiles(directory.Path));
        Assert.Equal(0, client.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("..\\escape")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n")]
    public async Task Operations_MaliciousIdentifiers_RejectBeforeClientAccess(string id) {
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(), client);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(id).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ExistsAsync(id).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.GetMetadataAsync(id).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.RemoveAsync(id).AsTask());
        Assert.Equal(0, client.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("../other")]
    [InlineData("cache//other")]
    [InlineData("cache/")]
    [InlineData("cache%2fother")]
    [InlineData("cache\\other")]
    public void Constructor_UnsafeNamespace_Rejects(string prefix) {
        using var client = new TestS3Client();
        Assert.ThrowsAny<ArgumentException>(() => new S3BlobStorage(Options() with { Prefix = prefix }, client));
    }

    [Fact]
    public void Constructor_UnsafeEndpointOrBounds_Rejects() {
        using var client = new TestS3Client();
        Assert.Throws<ArgumentException>(() => new S3BlobStorage(Options() with { Endpoint = new Uri("http://localhost:9000") }, client));
        Assert.Throws<ArgumentException>(() => new S3BlobStorage(Options() with { Endpoint = new Uri("https://user:secret@host") }, client));
        Assert.Throws<ArgumentOutOfRangeException>(() => new S3BlobStorage(Options() with { MaximumBlobBytes = long.MaxValue }, client));
        Assert.Throws<ArgumentOutOfRangeException>(() => new S3BlobStorage(Options() with { BufferBytes = 1 }, client));
        Assert.Throws<ArgumentOutOfRangeException>(() => new S3BlobStorage(Options() with { ListPageSize = 0 }, client));
    }

    [Fact]
    public async Task OpenReadAsync_EofVerificationAndDisposal_OwnsSdkResponseStream() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        var body = Encoding.UTF8.GetBytes("verified streaming body");
        PutObjectRequest? put = null;
        client.Put = (request, _) => { put = request; return Task.FromResult(new PutObjectResponse()); };
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        var id = await storage.StoreAsync(new MemoryStream(body));
        var tracked = new TrackedStream(body);
        client.Get = (_, _) => Task.FromResult(Response(put!, tracked, body.Length));
        var stream = await storage.OpenReadAsync(id);
        Assert.Equal(0, await stream.ReadAsync(Memory<byte>.Empty));
        Assert.False(tracked.Disposed);
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        Assert.Equal(body, output.ToArray());
        await stream.DisposeAsync();
        stream.Dispose();
        Assert.True(tracked.Disposed);
        Assert.Equal(1, tracked.DisposeCount);
    }

    [Theory]
    [InlineData("tampered", 8)]
    [InlineData("short", 8)]
    [InlineData("too-long-content", 8)]
    public async Task OpenReadAsync_TamperedOrTruncatedBody_ThrowsAndDisposes(string content, int declared) {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        PutObjectRequest? put = null;
        client.Put = (request, _) => { put = request; return Task.FromResult(new PutObjectResponse()); };
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        var id = await storage.StoreAsync(new MemoryStream(Encoding.ASCII.GetBytes("original")));
        var tracked = new TrackedStream(Encoding.ASCII.GetBytes(content));
        client.Get = (_, _) => Task.FromResult(Response(put!, tracked, declared));
        await using var stream = await storage.OpenReadAsync(id);
        await Assert.ThrowsAsync<InvalidDataException>(() => stream.CopyToAsync(Stream.Null));
        Assert.True(tracked.Disposed);
    }

    [Fact]
    public async Task OpenReadAsync_ExactLengthHashFailureOrCancellation_ClosesResponseWithoutRequiringEofRead() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        PutObjectRequest? put = null;
        client.Put = (request, _) => { put = request; return Task.FromResult(new PutObjectResponse()); };
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        var id = await storage.StoreAsync(new MemoryStream([1, 2, 3]));
        var corrupt = new TrackedStream([3, 2, 1]);
        client.Get = (_, _) => Task.FromResult(Response(put!, corrupt, 3));
        await using var stream = await storage.OpenReadAsync(id);
        await Assert.ThrowsAsync<InvalidDataException>(() => stream.ReadExactlyAsync(new byte[3]).AsTask());
        Assert.True(corrupt.Disposed);
        var cancelledBody = new TrackedStream([1, 2, 3]);
        client.Get = (_, _) => Task.FromResult(Response(put!, cancelledBody, 3));
        await using var cancelledStream = await storage.OpenReadAsync(id);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledStream.ReadAsync(new byte[3], cancellation.Token).AsTask());
        Assert.True(cancelledBody.Disposed);
    }

    [Fact]
    public async Task StoreAndRead_EmptyBlobWithZeroLimit_VerifiesEmptyDigest() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        PutObjectRequest? put = null;
        client.Put = (request, _) => { put = request; return Task.FromResult(new PutObjectResponse()); };
        await using var storage = new S3BlobStorage(Options(directory.Path) with { MaximumBlobBytes = 0 }, client);
        var id = await storage.StoreAsync(new MemoryStream());
        var tracked = new TrackedStream([]);
        client.Get = (_, _) => Task.FromResult(Response(put!, tracked, 0));
        await using var stream = await storage.OpenReadAsync(id);
        Assert.Equal(0, await stream.ReadAsync(new byte[1]));
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task OpenReadAsync_ForeignObjectOrMalformedOwnedMetadata_DisposesResponse() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(directory.Path), client);
        var foreign = new TrackedStream([1]);
        client.Get = (_, _) => Task.FromResult(new GetObjectResponse { ResponseStream = foreign, ContentLength = 1 });
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(new string('a', 32)).AsTask());
        Assert.True(foreign.Disposed);
        PutObjectRequest? put = null;
        client.Put = (request, _) => { put = request; return Task.FromResult(new PutObjectResponse()); };
        var id = await storage.StoreAsync(new MemoryStream([1]));
        var malformed = new TrackedStream([1]);
        var response = Response(put!, malformed, 1);
        response.Metadata["backflow-sha256"] = "invalid";
        client.Get = (_, _) => Task.FromResult(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.OpenReadAsync(id).AsTask());
        Assert.True(malformed.Disposed);
    }

    [Fact]
    public async Task MissingObject_ConfirmsBucket_WhilePermissionAndServiceFailuresPropagate() {
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(), client);
        var id = new string('b', 32);
        client.Head = (_, _) => Task.FromException<GetObjectMetadataResponse>(Error(HttpStatusCode.NotFound, "NotFound"));
        client.BucketHead = (_, _) => Task.FromResult(new HeadBucketResponse());
        Assert.False(await storage.ExistsAsync(id));
        Assert.Null(await storage.GetMetadataAsync(id));
        Assert.False(await storage.RemoveAsync(id));
        client.Get = (_, _) => Task.FromException<GetObjectResponse>(Error(HttpStatusCode.NotFound, "NoSuchKey"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(id).AsTask());
        client.Head = (_, _) => Task.FromException<GetObjectMetadataResponse>(Error(HttpStatusCode.Forbidden, "AccessDenied"));
        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ExistsAsync(id).AsTask());
        client.Head = (_, _) => Task.FromException<GetObjectMetadataResponse>(Error(HttpStatusCode.ServiceUnavailable, "SlowDown"));
        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.GetMetadataAsync(id).AsTask());
        client.Head = (_, _) => Task.FromException<GetObjectMetadataResponse>(Error(HttpStatusCode.NotFound, "NotFound"));
        client.BucketHead = (_, _) => Task.FromException<HeadBucketResponse>(Error(HttpStatusCode.Forbidden, "AccessDenied"));
        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.RemoveAsync(id).AsTask());
        var unknown = Error(HttpStatusCode.NotFound, "UnexpectedServiceFailure");
        client.Head = (_, _) => Task.FromException<GetObjectMetadataResponse>(unknown);
        Assert.Same(unknown, await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ExistsAsync(id).AsTask()));
        var missingBucket = Error(HttpStatusCode.NotFound, "NoSuchBucket");
        client.Head = (_, _) => Task.FromException<GetObjectMetadataResponse>(missingBucket);
        Assert.Same(missingBucket, await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ExistsAsync(id).AsTask()));
    }

    [Fact]
    public async Task ClearAsync_PaginatesAndRevalidatesOwnership_DeletesOnlyOwnedImmutableKeys() {
        using var directory = new TemporaryDirectory();
        using var client = new TestS3Client();
        var puts = new List<PutObjectRequest>();
        client.Put = (request, _) => { puts.Add(request); return Task.FromResult(new PutObjectResponse()); };
        await using var storage = new S3BlobStorage(Options(directory.Path) with { ListPageSize = 2 }, client);
        await storage.StoreAsync(new MemoryStream([1]));
        await storage.StoreAsync(new MemoryStream([2]));
        const string prefix = "test/cache/backflow-v1/";
        var foreign = prefix + new string('f', 32);
        var pages = 0;
        client.List = (request, _) => {
            Assert.Equal(prefix, request.Prefix);
            Assert.Equal(2, request.MaxKeys);
            pages++;
            if (pages == 1) {
                Assert.Null(request.ContinuationToken);
                return Task.FromResult(new ListObjectsV2Response {
                    IsTruncated = true, NextContinuationToken = "page-two",
                    S3Objects = [new S3Object { Key = puts[0].Key }, new S3Object { Key = foreign },
                        new S3Object { Key = "outside/" + new string('a', 32) }, new S3Object { Key = prefix + "../escape" }]
                });
            }
            Assert.Equal("page-two", request.ContinuationToken);
            return Task.FromResult(new ListObjectsV2Response { IsTruncated = false, S3Objects = [new S3Object { Key = puts[1].Key }] });
        };
        client.Head = (request, _) => {
            if (request.Key == foreign) return Task.FromResult(new GetObjectMetadataResponse());
            var put = Assert.Single(puts, value => value.Key == request.Key);
            return Task.FromResult(Head(put));
        };
        var deleted = new List<string>();
        client.Delete = (request, _) => {
            Assert.Equal("\"immutable-etag\"", request.IfMatch);
            deleted.Add(request.Key);
            return Task.FromResult(new DeleteObjectResponse());
        };
        await storage.ClearAsync();
        Assert.Equal(2, pages);
        Assert.Equal(puts.Select(value => value.Key), deleted);
        Assert.False(await storage.RemoveAsync(foreign[prefix.Length..]));
    }

    [Fact]
    public async Task ClearAsync_InvalidPaginationOrConditionalDeleteFailure_Propagates() {
        using var client = new TestS3Client();
        await using var storage = new S3BlobStorage(Options(), client);
        client.List = (_, _) => Task.FromResult(new ListObjectsV2Response { IsTruncated = true });
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.ClearAsync().AsTask());
        using var directory = new TemporaryDirectory();
        PutObjectRequest? captured = null;
        client.Put = (request, _) => { captured = request; return Task.FromResult(new PutObjectResponse()); };
        await using var other = new S3BlobStorage(Options(directory.Path), client);
        var id = await other.StoreAsync(new MemoryStream([1]));
        client.Head = (_, _) => Task.FromResult(Head(captured!));
        client.Delete = (_, _) => Task.FromException<DeleteObjectResponse>(Error(HttpStatusCode.PreconditionFailed, "PreconditionFailed"));
        await Assert.ThrowsAsync<AmazonS3Exception>(() => other.RemoveAsync(id).AsTask());
    }

    [Fact]
    public async Task Dispose_InjectedClientOwnership_IsExplicitAndAdapterRejectsFurtherOperations() {
        using var client = new TestS3Client();
        var storage = new S3BlobStorage(Options(), client);
        await storage.DisposeAsync();
        storage.Dispose();
        Assert.False(client.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => storage.ExistsAsync(new string('a', 32)).AsTask());
        var owned = new S3BlobStorage(Options(), client, ownsClient: true);
        owned.Dispose();
        Assert.True(client.Disposed);
    }

    private static S3BlobStorageOptions Options(string? directory = null) => new() {
        Name = "test", Bucket = "backflow-test", Prefix = "test/cache", TemporaryDirectory = directory ?? Path.GetTempPath()
    };

    private static AmazonS3Exception Error(HttpStatusCode status, string code) => new("Injected S3 failure") { StatusCode = status, ErrorCode = code };

    private static GetObjectResponse Response(PutObjectRequest put, Stream body, long length) {
        var response = new GetObjectResponse { ResponseStream = body, ContentLength = length };
        foreach (var key in put.Metadata.Keys) response.Metadata[key] = put.Metadata[key];
        return response;
    }

    private static GetObjectMetadataResponse Head(PutObjectRequest put) {
        var response = new GetObjectMetadataResponse { ContentLength = long.Parse(put.Metadata["backflow-length"]), ETag = "\"immutable-etag\"" };
        foreach (var key in put.Metadata.Keys) response.Metadata[key] = put.Metadata[key];
        return response;
    }

    private sealed class TestS3Client() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://unused.invalid" }) {
        public Func<PutObjectRequest, CancellationToken, Task<PutObjectResponse>> Put { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected PUT");
        public Func<GetObjectRequest, CancellationToken, Task<GetObjectResponse>> Get { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected GET");
        public Func<GetObjectMetadataRequest, CancellationToken, Task<GetObjectMetadataResponse>> Head { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected HEAD");
        public Func<HeadBucketRequest, CancellationToken, Task<HeadBucketResponse>> BucketHead { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected bucket HEAD");
        public Func<DeleteObjectRequest, CancellationToken, Task<DeleteObjectResponse>> Delete { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected DELETE");
        public Func<ListObjectsV2Request, CancellationToken, Task<ListObjectsV2Response>> List { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected LIST");
        public int Requests { get; private set; }
        public int PutCalls { get; private set; }
        public bool Disposed { get; private set; }
        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default) { Requests++; PutCalls++; return Put(request, cancellationToken); }
        public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default) { Requests++; return Get(request, cancellationToken); }
        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = default) { Requests++; return Head(request, cancellationToken); }
        public override Task<HeadBucketResponse> HeadBucketAsync(HeadBucketRequest request, CancellationToken cancellationToken = default) { Requests++; return BucketHead(request, cancellationToken); }
        public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default) { Requests++; return Delete(request, cancellationToken); }
        public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default) { Requests++; return List(request, cancellationToken); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TemporaryDirectory : IDisposable {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "backflow-s3-test-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class NonseekableStream(byte[] bytes) : MemoryStream(bytes) {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }

    private sealed class BrokenInputStream : MemoryStream {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Injected input failure"));
    }

    private sealed class CancellingInputStream(CancellationTokenSource cancellation) : MemoryStream {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            buffer.Span[0] = 1;
            cancellation.Cancel();
            return ValueTask.FromResult(1);
        }
    }

    private sealed class TrackedStream(byte[] bytes) : MemoryStream(bytes) {
        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; DisposeCount++; base.Dispose(disposing); }
    }
}
