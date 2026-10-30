using Shiron.Backflow.Samples.Types.Meta;
using Shiron.Backflow.Types;
using SkiaSharp;

namespace Shiron.Backflow.Samples.Imaging;

internal static class SampleImageCodec {
    internal static async ValueTask<(SKBitmap Bitmap, ImageMeta Meta)> DecodeAsync(IBufferData storage, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        using (var source = storage.OpenRead()) {
            await source.CopyToAsync(buffer, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();

        using var data = SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, checked((int) buffer.Length)));
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("The input is not a supported image.");
        var info = codec.Info.WithColorType(SKColorType.Rgba8888)
            .WithAlphaType(codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        try {
            if (bitmap.GetPixels() == IntPtr.Zero || codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new InvalidDataException("The image pixels could not be decoded completely.");
            var meta = Describe(info, codec.EncodedFormat, Math.Max(codec.FrameCount, 1));
            cancellationToken.ThrowIfCancellationRequested();
            return (bitmap, meta);
        } catch {
            bitmap.Dispose();
            throw;
        }
    }

    internal static IBlob<ImageMeta, IBufferData> EncodePng(SKBitmap bitmap, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("The processed image could not be encoded as PNG.");
        var bytes = encoded.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new Blob<ImageMeta, IBufferData>(Describe(bitmap.Info, SKEncodedImageFormat.Png, 1), new BufferData(bytes));
    }

    private static ImageMeta Describe(SKImageInfo info, SKEncodedImageFormat format, int frameCount) => new(
        info.Width, info.Height, format.ToString().ToUpperInvariant(), info.ColorType.ToString(), info.BitsPerPixel,
        HasAlpha: info.AlphaType != SKAlphaType.Opaque, IsAnimated: frameCount > 1,
        FrameCount: frameCount > 1 ? frameCount : null);
}
