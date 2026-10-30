using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Samples.Nodes;
using Shiron.Backflow.Samples.Types.Meta;
using Shiron.Backflow.Types;
using SkiaSharp;
using Xunit;

namespace Shiron.Backflow.Samples.Tests;

/// <summary>Real native codec/pixel integration checks: codec compatibility and native ownership cannot be established with codec test doubles.</summary>
public sealed class ImageProcessingTests {
    /// <summary>Checks decoding validates the actual PNG and retains caller ownership of the encoded buffer.</summary>
    [Fact]
    public async Task DecodeImage_Png_PreservesStoragePixelsAndMetadata() {
        var bytes = CreatePng(3, 2, (x, y) => new SKColor((byte) (x * 50), (byte) (y * 80), 123, 128));
        using var source = new ObservedBuffer(bytes.ToArray());
        var result = await ExecuteAsync(new DecodeImageNode(), source);
        Assert.Same(source, result.Storage);
        Assert.Equal(bytes, result.Storage.Data.ToArray());
        Assert.Equal(3, result.Meta.Width);
        Assert.Equal(2, result.Meta.Height);
        Assert.Equal("PNG", result.Meta.Format);
        Assert.Equal("Rgba8888", result.Meta.PixelFormat);
        Assert.Equal(32, result.Meta.BitsPerPixel);
        Assert.True(result.Meta.HasAlpha);
        Assert.False(result.Meta.IsAnimated);
        Assert.Null(result.Meta.FrameCount);
        Assert.Null(result.Meta.DpiX);
        Assert.Null(result.Meta.DpiY);
        Assert.Equal(1, source.OpenCount);
        Assert.Equal(1, source.ClosedCount);
        Assert.False(source.Disposed);
        using var image = SKBitmap.Decode(result.Storage.Data.Span);
        using var original = SKBitmap.Decode(bytes);
        Assert.Equal(original.GetPixel(2, 1), image.GetPixel(2, 1));
        Assert.InRange(image.GetPixel(2, 1).Blue, (byte) 122, (byte) 124);
    }

    /// <summary>Checks BT.709 primary-color values, alpha and updated PNG metadata, not just successful execution.</summary>
    [Fact]
    public async Task GrayScale_Png_ComputesLuminanceAndPreservesAlpha() {
        SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue, new(200, 100, 50, 128), SKColors.White, new(75, 75, 75)];
        using var source = new ObservedBuffer(CreatePng(3, 2, (x, y) => colors[y * 3 + x]));
        var grayscale = await ExecuteAsync(new GrayScaleNode(), source);
        AssertPngMeta(grayscale.Meta, 3, 2);
        using var result = grayscale.Storage;
        Assert.Equal(1, source.OpenCount);
        Assert.Equal(1, source.ClosedCount);
        Assert.False(source.Disposed);
        source.Dispose();
        using var image = SKBitmap.Decode(result.Data.Span);
        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        byte[] expected = [54, 182, 18, 118, 255, 75];
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++) {
                var index = y * image.Width + x;
                var pixel = image.GetPixel(x, y);
                Assert.Equal(expected[index], pixel.Red);
                Assert.Equal(pixel.Red, pixel.Green);
                Assert.Equal(pixel.Red, pixel.Blue);
                Assert.Equal(colors[index].Alpha, pixel.Alpha);
            }
    }

    /// <summary>Checks a real JPEG is decoded without alpha and transformed into a readable grayscale PNG.</summary>
    [Fact]
    public async Task DecodeAndGrayScale_Jpeg_PreserveDimensionsAndEncodePng() {
        using var source = new ObservedBuffer(CreateImage(6, 4, (_, _) => SKColors.Red, SKEncodedImageFormat.Jpeg));
        var decoded = await ExecuteAsync(new DecodeImageNode(), source);
        Assert.Equal("JPEG", decoded.Meta.Format);
        Assert.False(decoded.Meta.HasAlpha);
        Assert.Equal(6, decoded.Meta.Width);
        Assert.Equal(4, decoded.Meta.Height);
        var result = await ExecuteAsync(new GrayScaleNode(), source);
        using var storage = result.Storage;
        AssertPngMeta(result.Meta, 6, 4);
        using var image = SKBitmap.Decode(storage.Data.Span);
        Assert.InRange(image.GetPixel(2, 2).Red, (byte) 53, (byte) 55);
        Assert.Equal(image.GetPixel(2, 2).Red, image.GetPixel(2, 2).Green);
        Assert.Equal(image.GetPixel(2, 2).Red, image.GetPixel(2, 2).Blue);
        Assert.Equal(byte.MaxValue, image.GetPixel(2, 2).Alpha);
        Assert.Equal(2, source.ClosedCount);
    }

    /// <summary>Checks the sigma-one impulse response against an independently calculated normalized Gaussian kernel.</summary>
    [Fact]
    public async Task Blur_Impulse_ComputesGaussianPixelsWithoutChangingDimensions() {
        using var source = new ObservedBuffer(CreatePng(9, 9, (x, y) => x == 4 && y == 4 ? SKColors.White : SKColors.Black));
        var result = await ExecuteAsync(new BlurNode(), source, radius: 1);
        source.Dispose();
        using var output = result.Storage;
        using var image = SKBitmap.Decode(output.Data.Span);
        Assert.Equal(9, image.Width);
        Assert.Equal(9, image.Height);
        AssertPngMeta(result.Meta, 9, 9);
        var weights = Enumerable.Range(-3, 7).Select(x => Math.Exp(-(x * x) / 2d)).ToArray();
        var total = weights.Sum();
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++) {
                var expected = x is >= 1 and <= 7 && y is >= 1 and <= 7
                    ? (int) Math.Round(255 * weights[x - 1] * weights[y - 1] / (total * total)) : 0;
                var pixel = image.GetPixel(x, y);
                Assert.Equal((byte) expected, pixel.Red);
                Assert.Equal(pixel.Red, pixel.Green);
                Assert.Equal(pixel.Red, pixel.Blue);
                Assert.Equal(byte.MaxValue, pixel.Alpha);
                Assert.Equal(pixel, image.GetPixel(8 - x, y));
                Assert.Equal(pixel, image.GetPixel(x, 8 - y));
            }
        Assert.InRange(image.GetPixel(4, 4).Red, (byte) 1, (byte) 254);
        Assert.True(image.GetPixel(3, 4).Red > 0);
        Assert.Equal(1, source.ClosedCount);
    }

    /// <summary>Checks clamped edges preserve a constant translucent image without darkening or fading.</summary>
    [Fact]
    public async Task Blur_UniformTranslucentImage_PreservesEdgePixels() {
        var color = new SKColor(90, 120, 180, 128);
        using var source = new ObservedBuffer(CreatePng(5, 3, (_, _) => color));
        var result = await ExecuteAsync(new BlurNode(), source, radius: 2);
        using var output = result.Storage;
        using var image = SKBitmap.Decode(output.Data.Span);
        AssertPngMeta(result.Meta, 5, 3);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++) {
                var pixel = image.GetPixel(x, y);
                Assert.InRange(pixel.Red, (byte) 89, (byte) 91);
                Assert.InRange(pixel.Green, (byte) 119, (byte) 121);
                Assert.InRange(pixel.Blue, (byte) 179, (byte) 181);
                Assert.Equal(color.Alpha, pixel.Alpha);
            }
    }

    /// <summary>Checks premultiplied blur does not spread hidden colors from fully transparent pixels.</summary>
    [Fact]
    public async Task Blur_TransparentBoundary_DoesNotBleedHiddenColor() {
        using var source = new ObservedBuffer(CreatePng(9, 3, (x, _) => x < 4 ? SKColors.White : new SKColor(255, 0, 0, 0)));
        using var output = (await ExecuteAsync(new BlurNode(), source, radius: 1)).Storage;
        using var image = SKBitmap.Decode(output.Data.Span);
        var edge = image.GetPixel(4, 1);
        Assert.InRange(edge.Alpha, (byte) 1, (byte) 254);
        Assert.Equal(edge.Red, edge.Green);
        Assert.Equal(edge.Red, edge.Blue);
    }

    /// <summary>Checks zero sigma is a pixel-preserving PNG re-encode with accurate output metadata.</summary>
    [Fact]
    public async Task Blur_ZeroSigma_PreservesAllPixels() {
        using var source = new ObservedBuffer(CreatePng(4, 3, (x, y) => new SKColor((byte) (x * 60), (byte) (y * 90), 50, 128)));
        using var original = SKBitmap.Decode(source.Data.Span);
        var result = await ExecuteAsync(new BlurNode(), source, radius: 0);
        using var output = result.Storage;
        using var actual = SKBitmap.Decode(output.Data.Span);
        AssertPngMeta(result.Meta, 4, 3);
        for (var y = 0; y < actual.Height; y++)
            for (var x = 0; x < actual.Width; x++)
                Assert.Equal(original.GetPixel(x, y), actual.GetPixel(x, y));
    }

    /// <summary>Checks invalid sigma is rejected before opening image storage.</summary>
    [Fact]
    public async Task Blur_NegativeSigma_RejectsWithoutReadingStorage() {
        using var source = new ObservedBuffer(CreatePng(1, 1, (_, _) => SKColors.Red));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ExecuteAsync(new BlurNode(), source, radius: -1));
        Assert.Equal(0, source.OpenCount);
        Assert.False(source.Disposed);
    }

    /// <summary>Checks pre-cancellation prevents storage reads and produces no output.</summary>
    [Theory]
    [InlineData(typeof(DecodeImageNode))]
    [InlineData(typeof(GrayScaleNode))]
    [InlineData(typeof(BlurNode))]
    public async Task Execute_PreCanceled_DoesNotReadOrPublish(Type nodeType) {
        var node = CreateNode(nodeType);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var source = new ObservedBuffer(CreatePng(1, 1, (_, _) => SKColors.White));
        var context = Bind(node, source, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => node.ExecuteAsync(context).AsTask());
        Assert.Equal(0, source.OpenCount);
        Assert.False(context.HasAny(node.Outputs.Single()));
    }

    /// <summary>Checks cancellation during asynchronous input reads closes the owned stream without disposing caller storage.</summary>
    [Theory]
    [InlineData(typeof(DecodeImageNode))]
    [InlineData(typeof(GrayScaleNode))]
    [InlineData(typeof(BlurNode))]
    public async Task Execute_CanceledDuringRead_ClosesStreamAndDoesNotPublish(Type nodeType) {
        var node = CreateNode(nodeType);
        using var cancellation = new CancellationTokenSource();
        using var source = new ObservedBuffer(CreatePng(3, 2, (_, _) => SKColors.White), cancellation.Cancel);
        var context = Bind(node, source, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => node.ExecuteAsync(context).AsTask());
        Assert.Equal(1, source.OpenCount);
        Assert.Equal(1, source.ClosedCount);
        Assert.False(source.Disposed);
        Assert.False(context.HasAny(node.Outputs.Single()));
    }

    /// <summary>Checks malformed and truncated encoded data cannot produce metadata or partially decoded output.</summary>
    [Theory]
    [InlineData(typeof(DecodeImageNode), false)]
    [InlineData(typeof(GrayScaleNode), false)]
    [InlineData(typeof(BlurNode), false)]
    [InlineData(typeof(DecodeImageNode), true)]
    [InlineData(typeof(GrayScaleNode), true)]
    [InlineData(typeof(BlurNode), true)]
    public async Task Execute_InvalidImage_RejectsAndClosesStream(Type nodeType, bool truncated) {
        var bytes = truncated ? CreatePng(10, 10, (x, y) => new SKColor((byte) x, (byte) y, 123))[..45] : new byte[] { 1, 2, 3, 4 };
        using var source = new ObservedBuffer(bytes);
        var node = CreateNode(nodeType);
        var context = Bind(node, source);
        await Assert.ThrowsAsync<InvalidDataException>(() => node.ExecuteAsync(context).AsTask());
        Assert.Equal(1, source.ClosedCount);
        Assert.False(source.Disposed);
        Assert.False(context.HasAny(node.Outputs.Single()));
    }

    /// <summary>Checks an input read failure propagates while releasing the opened stream and withholding output.</summary>
    [Theory]
    [InlineData(typeof(DecodeImageNode))]
    [InlineData(typeof(GrayScaleNode))]
    [InlineData(typeof(BlurNode))]
    public async Task Execute_ReadFailure_ClosesStreamAndDoesNotPublish(Type nodeType) {
        using var source = new ObservedBuffer(CreatePng(1, 1, (_, _) => SKColors.White), () => throw new IOException("Input read failed."));
        var node = CreateNode(nodeType);
        var context = Bind(node, source);
        await Assert.ThrowsAsync<IOException>(() => node.ExecuteAsync(context).AsTask());
        Assert.Equal(1, source.ClosedCount);
        Assert.False(source.Disposed);
        Assert.False(context.HasAny(node.Outputs.Single()));
    }

    /// <summary>Checks repeated native processing leaves independent readable outputs after all input storage is destroyed.</summary>
    [Fact]
    public async Task Execute_RepeatedProcessing_OutputBuffersOutliveNativeAndInputResources() {
        for (var iteration = 0; iteration < 24; iteration++) {
            using var source = new ObservedBuffer(CreatePng(8, 5, (_, _) => SKColors.Lime));
            var grayscale = await ExecuteAsync(new GrayScaleNode(), source);
            source.Dispose();
            using var grayStorage = grayscale.Storage;
            using var nextSource = new ObservedBuffer(grayStorage.Data.ToArray());
            var blurred = await ExecuteAsync(new BlurNode(), nextSource, radius: 1);
            nextSource.Dispose();
            using var output = blurred.Storage;
            using var image = SKBitmap.Decode(output.Data.Span);
            Assert.Equal(new SKColor(182, 182, 182), image.GetPixel(0, 0));
            Assert.Equal(1, source.ClosedCount);
            Assert.Equal(1, nextSource.ClosedCount);
        }
    }

    private static AbstractNode CreateNode(Type type) =>
        (AbstractNode) (Activator.CreateInstance(type) ?? throw new InvalidOperationException("Cannot create image node."));

    private static async Task<IBlob<ImageMeta, IBufferData>> ExecuteAsync(AbstractNode node, ObservedBuffer source, int radius = 1) {
        var context = Bind(node, source, radius: radius);
        Assert.Equal(NodeState.Done, await node.ExecuteAsync(context));
        return Assert.IsAssignableFrom<IBlob<ImageMeta, IBufferData>>(context.ReadAny(node.Outputs.Single()));
    }

    private static NodeContext Bind(AbstractNode node, ObservedBuffer source, CancellationToken cancellationToken = default, int radius = 1) {
        var ports = node.Ports.ToArray();
        var context = new NodeContext(ArrayPipelineContext.Create(ports.Select(port => port.PortType).ToArray()),
            ports.Select((port, index) => (Port: (IPort) port, Index: index)).ToDictionary(pair => pair.Port, pair => pair.Index),
            new Dictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>(), ct: cancellationToken);
        var blob = new Blob<ImageMeta, IBufferData>(new ImageMeta(99, 99, "JPEG", IsAnimated: true, FrameCount: 7), source);
        if (node is DecodeImageNode decoder) context.Write<IBlob>(decoder.In, blob);
        if (node is GrayScaleNode grayscale) context.Write<IBlob<ImageMeta, IBufferData>>(grayscale.In, blob);
        if (node is BlurNode blur) {
            context.Write<IBlob<ImageMeta, IBufferData>>(blur.In, blob);
            context.Write(blur.Radius, radius);
        }
        return context;
    }

    private static byte[] CreatePng(int width, int height, Func<int, int, SKColor> color) =>
        CreateImage(width, height, color, SKEncodedImageFormat.Png);

    private static byte[] CreateImage(int width, int height, Func<int, int, SKColor> color, SKEncodedImageFormat format) {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) bitmap.SetPixel(x, y, color(x, y));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 100);
        return encoded.ToArray();
    }

    private static void AssertPngMeta(ImageMeta meta, int width, int height) {
        Assert.Equal(width, meta.Width);
        Assert.Equal(height, meta.Height);
        Assert.Equal("PNG", meta.Format);
        Assert.Equal(32, meta.BitsPerPixel);
        Assert.False(meta.IsAnimated);
        Assert.Null(meta.FrameCount);
    }

    private sealed class ObservedBuffer(byte[] bytes, Action? onRead = null) : IBufferData {
        internal int OpenCount { get; private set; }
        internal int ClosedCount { get; private set; }
        internal bool Disposed { get; private set; }
        public ReadOnlyMemory<byte> Data => Disposed ? throw new ObjectDisposedException(nameof(ObservedBuffer)) : bytes;
        public Stream OpenRead() {
            ObjectDisposedException.ThrowIf(Disposed, this);
            OpenCount++;
            return new ObservedStream(bytes, () => ClosedCount++, onRead);
        }
        public void Dispose() {
            Disposed = true;
            Array.Clear(bytes);
        }
    }

    private sealed class ObservedStream(byte[] bytes, Action onDispose, Action? onRead) : Stream {
        private readonly MemoryStream _source = new(bytes, writable: false);
        private bool _disposed;
        public override bool CanRead => !_disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _source.Length;
        public override long Position { get => _source.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) {
            onRead?.Invoke();
            return _source.Read(buffer, offset, count);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            onRead?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return _source.ReadAsync(buffer, cancellationToken);
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) {
            if (disposing && !_disposed) {
                _disposed = true;
                _source.Dispose();
                onDispose();
            }
            base.Dispose(disposing);
        }
    }
}
