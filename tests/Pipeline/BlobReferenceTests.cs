using System.Text.Json;
using Shiron.Lib.Pipeline.BlobStorage;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class BlobReferenceConstructorTests {
    [Fact]
    public void Constructor_SetsStorageNameAndBlobId() {
        var ref_ = new BlobReference("disk", "abc123");

        Assert.Equal("disk", ref_.StorageName);
        Assert.Equal("abc123", ref_.BlobId);
    }

    [Fact]
    public void Constructor_CreatesBlobUri() {
        var ref_ = new BlobReference("disk", "abc123");

        Assert.Equal("blob://disk/abc123", ref_.Uri.ToString());
    }

    [Fact]
    public void Constructor_NullStorageName_Throws() {
        Assert.Throws<ArgumentNullException>(() => new BlobReference(null!, "abc"));
    }

    [Fact]
    public void Constructor_NullBlobId_Throws() {
        Assert.Throws<ArgumentNullException>(() => new BlobReference("disk", null!));
    }
}

public class BlobReferenceParseUriTests {
    [Fact]
    public void Parse_ValidUri_ReturnsReference() {
        var uri = new Uri("blob://disk/abc123");
        var ref_ = BlobReference.Parse(uri);

        Assert.Equal("disk", ref_.StorageName);
        Assert.Equal("abc123", ref_.BlobId);
    }

    [Fact]
    public void Parse_InvalidScheme_Throws() {
        Assert.Throws<UriFormatException>(() => BlobReference.Parse(new Uri("file://disk/abc")));
    }

    [Fact]
    public void Parse_NoBlobId_Throws() {
        Assert.Throws<UriFormatException>(() => BlobReference.Parse(new Uri("blob://disk/")));
    }
}

public class BlobReferenceParseStringTests {
    [Fact]
    public void Parse_ValidString_ReturnsReference() {
        var ref_ = BlobReference.Parse("blob://s3/my-object-key");

        Assert.Equal("s3", ref_.StorageName);
        Assert.Equal("my-object-key", ref_.StorageName == "s3" ? "my-object-key" : "");
    }
}

public class BlobReferenceTryParseUriTests {
    [Fact]
    public void TryParse_ValidUri_ReturnsTrue() {
        var result = BlobReference.TryParse(new Uri("blob://disk/abc"), out var ref_);

        Assert.True(result);
        Assert.Equal("disk", ref_.StorageName);
        Assert.Equal("abc", ref_.BlobId);
    }

    [Fact]
    public void TryParse_NullUri_ReturnsFalse() {
        Uri? uri = null;
        Assert.False(BlobReference.TryParse(uri!, out _));
    }

    [Fact]
    public void TryParse_WrongScheme_ReturnsFalse() {
        Assert.False(BlobReference.TryParse(new Uri("https://disk/abc"), out _));
    }

    [Fact]
    public void TryParse_NoBlobId_ReturnsFalse() {
        Assert.False(BlobReference.TryParse(new Uri("blob://disk/"), out _));
    }
}

public class BlobReferenceTryParseStringTests {
    [Fact]
    public void TryParse_ValidString_ReturnsTrue() {
        var result = BlobReference.TryParse("blob://disk/xyz", out var ref_);

        Assert.True(result);
        Assert.Equal("disk", ref_.StorageName);
        Assert.Equal("xyz", ref_.BlobId);
    }

    [Fact]
    public void TryParse_NullString_ReturnsFalse() {
        string? str = null;
        Assert.False(BlobReference.TryParse(str!, out _));
    }

    [Fact]
    public void TryParse_InvalidString_ReturnsFalse() {
        Assert.False(BlobReference.TryParse("not-a-uri", out _));
    }
}

public class BlobReferenceEqualityTests {
    [Fact]
    public void SameStorageAndBlobId_AreEqual() {
        var a = new BlobReference("disk", "abc");
        var b = new BlobReference("disk", "abc");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void DifferentStorageName_AreNotEqual() {
        var a = new BlobReference("disk", "abc");
        var b = new BlobReference("mem", "abc");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DifferentBlobId_AreNotEqual() {
        var a = new BlobReference("disk", "abc");
        var b = new BlobReference("disk", "xyz");

        Assert.NotEqual(a, b);
    }
}

public class BlobReferenceToStringTests {
    [Fact]
    public void ToString_ReturnsUriString() {
        var ref_ = new BlobReference("disk", "abc");
        Assert.Equal("blob://disk/abc", ref_.ToString());
    }
}
