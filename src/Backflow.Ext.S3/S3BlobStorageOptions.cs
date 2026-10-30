namespace Shiron.Backflow.Ext.S3;

/// <summary>Server-side S3 namespace and resource bounds; credentials belong in an injected client or credential provider.</summary>
public sealed record S3BlobStorageOptions {
    /// <summary>Gets the stable storage registry name, also used to identify owned objects.</summary>
    public required string Name { get; init; }
    /// <summary>Gets an existing bucket; the adapter never creates or deletes buckets.</summary>
    public required string Bucket { get; init; }
    /// <summary>Gets a nonempty, dedicated namespace of slash-separated ASCII letters, digits, hyphens or underscores.</summary>
    public required string Prefix { get; init; }
    /// <summary>Gets an optional S3-compatible service endpoint; null selects AWS regional endpoints.</summary>
    public Uri? Endpoint { get; init; }
    /// <summary>Gets the signing region for a factory-created client.</summary>
    public string Region { get; init; } = "us-east-1";
    /// <summary>Gets whether path-style addressing is used by a factory-created client.</summary>
    public bool ForcePathStyle { get; init; } = true;
    /// <summary>Gets whether an explicit HTTP endpoint is permitted, intended for isolated local deployments only.</summary>
    public bool AllowInsecureHttp { get; init; }
    /// <summary>Gets the maximum upload size in bytes; single atomic PUTs support at most five GiB.</summary>
    public long MaximumBlobBytes { get; init; } = 256L * 1024 * 1024;
    /// <summary>Gets the maximum concurrent upload spools per adapter instance.</summary>
    public int MaximumConcurrentUploads { get; init; } = 2;
    /// <summary>Gets the bounded upload buffer size in bytes.</summary>
    public int BufferBytes { get; init; } = 64 * 1024;
    /// <summary>Gets the maximum S3 listing page size.</summary>
    public int ListPageSize { get; init; } = 1000;
    /// <summary>Gets the local spool directory; it must be private to the server process.</summary>
    public string TemporaryDirectory { get; init; } = Path.GetTempPath();

    internal void Validate() {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (Name.Length > 200 || Name.Any(char.IsControl)) throw new ArgumentException("Invalid storage name.");
        ArgumentException.ThrowIfNullOrWhiteSpace(Bucket);
        if (Bucket.Length is < 3 or > 63 || !char.IsAsciiLetterOrDigit(Bucket[0]) || !char.IsAsciiLetterOrDigit(Bucket[^1])
            || Bucket.Any(value => !(value is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.'))
            || Bucket.Contains("..", StringComparison.Ordinal)) throw new ArgumentException("Invalid bucket name.");
        ArgumentException.ThrowIfNullOrWhiteSpace(Prefix);
        if (Prefix.Length > 200 || Prefix.Split('/').Any(segment => segment.Length == 0
            || segment.Any(value => !char.IsAsciiLetterOrDigit(value) && value is not '-' and not '_')))
            throw new ArgumentException("A dedicated safe, nonempty prefix is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(Region);
        if (Endpoint is not null && (!Endpoint.IsAbsoluteUri || Endpoint.UserInfo.Length != 0
            || Endpoint.Query.Length != 0 || Endpoint.Fragment.Length != 0 || Endpoint.AbsolutePath != "/"
            || (Endpoint.Scheme != Uri.UriSchemeHttps && !(AllowInsecureHttp && Endpoint.Scheme == Uri.UriSchemeHttp))))
            throw new ArgumentException("Endpoint must be HTTPS (or explicitly enabled HTTP), without credentials, path, query or fragment.");
        if (MaximumBlobBytes is < 0 or > 5L * 1024 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumBlobBytes));
        if (MaximumConcurrentUploads is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(MaximumConcurrentUploads));
        if (BufferBytes is < 4096 or > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(BufferBytes));
        if (ListPageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(ListPageSize));
        ArgumentException.ThrowIfNullOrWhiteSpace(TemporaryDirectory);
    }
}
