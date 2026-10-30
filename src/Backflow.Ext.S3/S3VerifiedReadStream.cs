using System.Security.Cryptography;
using Amazon.S3.Model;

namespace Shiron.Backflow.Ext.S3;

internal sealed class S3VerifiedReadStream(GetObjectResponse response, long expectedLength, byte[] expectedDigest) : Stream {
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _read;
    private bool _verified;
    private bool _disposed;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => expectedLength;
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.Length == 0) return 0;
        try {
            var count = response.ResponseStream.Read(buffer[..Limit(buffer.Length)]);
            Verify(buffer[..count]);
            return count;
        } catch { Dispose(); throw; }
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length == 0) return 0;
            var count = await response.ResponseStream.ReadAsync(buffer[..Limit(buffer.Length)], cancellationToken).ConfigureAwait(false);
            Verify(buffer.Span[..count]);
            return count;
        } catch { Dispose(); throw; }
    }
    private int Limit(int requested) => (int) Math.Min(requested, expectedLength - _read + 1);
    private void Verify(ReadOnlySpan<byte> data) {
        if (data.Length > 0) {
            _read += data.Length;
            if (_read > expectedLength) { Dispose(); throw new InvalidDataException("Blob exceeds its declared length."); }
            _hash.AppendData(data);
        }
        if (data.Length == 0 && _read != expectedLength) {
            Dispose();
            throw new InvalidDataException("Blob ended before its declared length.");
        }
        if (_read == expectedLength && !_verified) {
            if (!CryptographicOperations.FixedTimeEquals(_hash.GetHashAndReset(), expectedDigest)) {
                Dispose();
                throw new InvalidDataException("Blob length or SHA-256 verification failed.");
            }
            _verified = true;
        }
    }
    protected override void Dispose(bool disposing) {
        if (disposing && !_disposed) {
            _disposed = true;
            try { response.Dispose(); } finally { _hash.Dispose(); }
        }
        base.Dispose(disposing);
    }
    public override ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    public override void Flush() { ObjectDisposedException.ThrowIf(_disposed, this); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
