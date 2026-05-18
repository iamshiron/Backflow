namespace Shiron.Lib.Pipeline.BlobStorage;

public readonly record struct BlobReference {
    public string StorageName { get; }
    public string BlobId { get; }
    public Uri Uri { get; }

    public BlobReference(string storageName, string blobId) {
        StorageName = storageName ?? throw new ArgumentNullException(nameof(storageName));
        BlobId = blobId ?? throw new ArgumentNullException(nameof(blobId));
        Uri = new Uri($"blob://{storageName}/{blobId}");
    }

    public static BlobReference Parse(Uri uri) {
        if (!TryParse(uri, out var reference))
            throw new UriFormatException($"Invalid blob URI: '{uri}'");
        return reference;
    }

    public static BlobReference Parse(string uriString) {
        return Parse(new Uri(uriString));
    }

    public static bool TryParse(Uri uri, out BlobReference reference) {
        reference = default;
        if (uri is null) return false;

        if (uri.Scheme != "blob") return false;

        var storageName = uri.Host;
        if (string.IsNullOrEmpty(storageName)) return false;

        var blobId = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(blobId)) return false;

        reference = new BlobReference(storageName, blobId);
        return true;
    }

    public static bool TryParse(string uriString, out BlobReference reference) {
        reference = default;
        if (uriString is null) return false;
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri)) return false;
        return TryParse(uri, out reference);
    }

    public override string ToString() => Uri.ToString();
}
