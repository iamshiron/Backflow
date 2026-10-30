using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Shiron.Backflow.BlobStorage;
using Shiron.Backflow.Ext.S3;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.S3;

/// <summary>Opt-in S3 wire-semantic checks; atomic PUT/checksums, conditional DELETE and pagination require a real S3 implementation.</summary>
public sealed class RustFsIntegrationTests {
    [RustFsFact]
    public async Task Lifecycle_ActualIsolatedRustFs_ValidatesIntegrityConditionalDeleteAndScopedPaginatedClear() {
        var endpoint = new Uri(Required("BACKFLOW_RUSTFS_TEST_ENDPOINT"));
        var credentials = new BasicAWSCredentials(Required("BACKFLOW_RUSTFS_TEST_ACCESS_KEY"), Required("BACKFLOW_RUSTFS_TEST_SECRET_KEY"));
        using var client = new AmazonS3Client(credentials, new AmazonS3Config {
            ServiceURL = endpoint.AbsoluteUri, AuthenticationRegion = "us-east-1", ForcePathStyle = true
        });
        var bucket = "backflow-test-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "backflow-rustfs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        try {
            var options = new S3BlobStorageOptions {
                Name = "rustfs-validation", Bucket = bucket, Prefix = "owned/blobs", Endpoint = endpoint,
                AllowInsecureHttp = endpoint.Scheme == "http", ListPageSize = 2, TemporaryDirectory = directory
            };
            await using var storage = S3BlobStorage.Create(options, credentials);
            await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = "unrelated/sentinel", ContentBody = "untouched" });
            await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = "owned/blobs-other/sentinel", ContentBody = "untouched" });
            var foreignKey = "owned/blobs/backflow-v1/" + new string('f', 32);
            await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = foreignKey, ContentBody = "foreign-without-marker" });
            await using var otherOwner = new S3BlobStorage(options with { Name = "another-owner" }, client);
            var otherOwnerId = await otherOwner.StoreAsync(new MemoryStream([9]));
            var ids = new List<string>();
            for (var index = 0; index < 5; index++) {
                var bytes = Encoding.UTF8.GetBytes("rustfs-payload-" + index);
                var id = await storage.StoreAsync(new NonseekableStream(bytes), new BlobMetadata {
                    ContentType = "text/plain", ContentLength = bytes.Length,
                    Tags = new Dictionary<string, string> { ["description"] = "日本語 / S3 metadata" }
                });
                ids.Add(id);
                Assert.True(await storage.ExistsAsync(id));
                var metadata = await storage.GetMetadataAsync(id);
                Assert.Equal(bytes.Length, metadata!.ContentLength);
                Assert.Equal("text/plain", metadata.ContentType);
                Assert.Equal("日本語 / S3 metadata", metadata.Tags["description"]);
                await using var read = await storage.OpenReadAsync(id);
                using var output = new MemoryStream();
                await read.CopyToAsync(output);
                Assert.Equal(bytes, output.ToArray());
            }
            var large = new byte[2 * 1024 * 1024 + 17];
            new Random(42).NextBytes(large);
            var largeId = await storage.StoreAsync(new NonseekableStream(large));
            ids.Add(largeId);
            await using (var largeRead = await storage.OpenReadAsync(largeId)) {
                using var output = new MemoryStream();
                await largeRead.CopyToAsync(output);
                Assert.Equal(large, output.ToArray());
            }
            var emptyId = await storage.StoreAsync(new MemoryStream());
            ids.Add(emptyId);
            await using (var emptyRead = await storage.OpenReadAsync(emptyId)) Assert.Equal(0, await emptyRead.ReadAsync(new byte[1]));
            Assert.Empty(Directory.GetFiles(directory));
            var missing = new string('0', 32);
            Assert.False(await storage.ExistsAsync(missing));
            Assert.Null(await storage.GetMetadataAsync(missing));
            Assert.False(await storage.RemoveAsync(missing));
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(missing).AsTask());
            Assert.True(await storage.RemoveAsync(ids[0]));
            Assert.False(await storage.RemoveAsync(ids[0]));
            Assert.False(await storage.ExistsAsync(otherOwnerId));
            Assert.False(await storage.RemoveAsync(otherOwnerId));
            await using (var reopened = new S3BlobStorage(options, client)) Assert.True(await reopened.ExistsAsync(ids[1]));

            var corruptKey = "owned/blobs/backflow-v1/" + ids[1];
            var head = await client.GetObjectMetadataAsync(bucket, corruptKey);
            var overwrite = new PutObjectRequest { BucketName = bucket, Key = corruptKey, ContentBody = new string('x', (int)head.ContentLength) };
            foreach (var key in head.Metadata.Keys) overwrite.Metadata[key] = head.Metadata[key];
            await client.PutObjectAsync(overwrite);
            await using (var corrupted = await storage.OpenReadAsync(ids[1]))
                await Assert.ThrowsAsync<InvalidDataException>(() => corrupted.CopyToAsync(Stream.Null));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(new MemoryStream([1]), ct: cancelled.Token).AsTask());
            Assert.Empty(Directory.GetFiles(directory));
            await storage.ClearAsync();
            foreach (var id in ids) Assert.False(await storage.ExistsAsync(id));
            Assert.True(await otherOwner.ExistsAsync(otherOwnerId));
            var remaining = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket });
            Assert.Equal(4, remaining.S3Objects.Count);
            Assert.Contains(remaining.S3Objects, item => item.Key == foreignKey);
            Assert.Contains(remaining.S3Objects, item => item.Key == "unrelated/sentinel");
            Assert.Contains(remaining.S3Objects, item => item.Key == "owned/blobs-other/sentinel");

            using var invalidClient = new AmazonS3Client(new BasicAWSCredentials("invalid-test-access", "invalid-test-secret"),
                new AmazonS3Config { ServiceURL = endpoint.AbsoluteUri, AuthenticationRegion = "us-east-1", ForcePathStyle = true });
            await using var denied = new S3BlobStorage(options, invalidClient);
            var permission = await Assert.ThrowsAsync<AmazonS3Exception>(() => denied.ExistsAsync(ids[2]).AsTask());
            Assert.Equal(HttpStatusCode.Forbidden, permission.StatusCode);
        } finally {
            string? token = null;
            do {
                var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, ContinuationToken = token });
                foreach (var item in page.S3Objects ?? []) await client.DeleteObjectAsync(bucket, item.Key);
                if (page.IsTruncated != true) break;
                token = page.NextContinuationToken;
            } while (true);
            await client.DeleteBucketAsync(bucket);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Required(string key) => Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException("Missing isolated RustFS test configuration.");

    private sealed class RustFsFactAttribute : FactAttribute {
        public RustFsFactAttribute() {
            if (Environment.GetEnvironmentVariable("BACKFLOW_RUSTFS_TEST_ENDPOINT") is null)
                Skip = "Opt-in: requires a new disposable RustFS container, never existing user storage.";
        }
    }

    private sealed class NonseekableStream(byte[] data) : MemoryStream(data) {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
