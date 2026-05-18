using Shiron.Lib.Pipeline.BlobStorage;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class BlobMetadataDefaultTests {
    [Fact]
    public void Default_HasNullContentType() {
        var meta = new BlobMetadata();
        Assert.Null(meta.ContentType);
    }

    [Fact]
    public void Default_HasNullContentLength() {
        var meta = new BlobMetadata();
        Assert.Null(meta.ContentLength);
    }

    [Fact]
    public void Default_HasEmptyTags() {
        var meta = new BlobMetadata();
        Assert.Empty(meta.Tags);
    }
}

public class BlobMetadataInitTests {
    [Fact]
    public void Init_SetsContentType() {
        var meta = new BlobMetadata { ContentType = "image/png" };
        Assert.Equal("image/png", meta.ContentType);
    }

    [Fact]
    public void Init_SetsContentLength() {
        var meta = new BlobMetadata { ContentLength = 1024 };
        Assert.Equal(1024, meta.ContentLength);
    }

    [Fact]
    public void Init_SetsTags() {
        var meta = new BlobMetadata {
            Tags = new Dictionary<string, string> { ["source"] = "camera" }
        };
        Assert.Equal("camera", meta.Tags["source"]);
    }
}

public class BlobMetadataEqualityTests {
    [Fact]
    public void SameValues_AreEqual() {
        var tags = new Dictionary<string, string> { ["key"] = "val" };
        var a = new BlobMetadata { ContentType = "text/plain", ContentLength = 10, Tags = tags };
        var b = a with { };
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentContentType_AreNotEqual() {
        var a = new BlobMetadata { ContentType = "text/plain" };
        var b = new BlobMetadata { ContentType = "image/png" };
        Assert.NotEqual(a, b);
    }
}

public class BlobMetadataWithTests {
    [Fact]
    public void With_CreatesCopyWithModifiedContentLength() {
        var original = new BlobMetadata { ContentType = "text/plain", ContentLength = 10 };
        var modified = original with { ContentLength = 20 };

        Assert.Equal(10, original.ContentLength);
        Assert.Equal(20, modified.ContentLength);
        Assert.Equal("text/plain", modified.ContentType);
    }
}
