using System.Buffers;
using SkiaSharp;

namespace Shiron.Backflow.Samples.Imaging;

internal static class GaussianBlur {
    private const int ChannelsPerPixel = 4;
    private const int AlphaChannel = 3;
    private const int KernelSigmaExtent = 3;
    private const float MaximumChannelValue = byte.MaxValue;

    internal static SKBitmap Apply(SKBitmap source, int sigma, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sigma);
        cancellationToken.ThrowIfCancellationRequested();
        var radius = checked(sigma * KernelSigmaExtent);
        var weights = new float[checked(radius * 2 + 1)];
        var sum = 0d;
        for (var i = 0; i < weights.Length; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            var distance = (double) (i - radius) / sigma;
            var weight = Math.Exp(-distance * distance / 2);
            weights[i] = (float) weight;
            sum += weight;
        }
        for (var i = 0; i < weights.Length; i++) weights[i] /= (float) sum;

        var count = checked(source.Width * source.Height * ChannelsPerPixel);
        var intermediate = ArrayPool<float>.Shared.Rent(count);
        SKBitmap? result = null;
        try {
            var input = source.GetPixelSpan();
            var rowLength = checked(source.Width * ChannelsPerPixel);
            for (var y = 0; y < source.Height; y++) {
                for (var x = 0; x < source.Width; x++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = intermediate.AsSpan(y * rowLength + x * ChannelsPerPixel, ChannelsPerPixel);
                    target.Clear();
                    for (var k = 0; k < weights.Length; k++) {
                        var offset = y * source.RowBytes + Math.Clamp(x + k - radius, 0, source.Width - 1) * ChannelsPerPixel;
                        var alpha = input[offset + AlphaChannel];
                        var weight = weights[k];
                        for (var channel = 0; channel < AlphaChannel; channel++)
                            target[channel] += input[offset + channel] * (alpha / MaximumChannelValue) * weight;
                        target[AlphaChannel] += alpha * weight;
                    }
                }
            }

            result = new SKBitmap(source.Info);
            if (result.GetPixels() == IntPtr.Zero) throw new InvalidOperationException("Cannot allocate blurred image pixels.");
            var output = result.GetPixelSpan();
            Span<float> accumulated = stackalloc float[ChannelsPerPixel];
            for (var y = 0; y < source.Height; y++) {
                for (var x = 0; x < source.Width; x++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    accumulated.Clear();
                    for (var k = 0; k < weights.Length; k++) {
                        var offset = Math.Clamp(y + k - radius, 0, source.Height - 1) * rowLength + x * ChannelsPerPixel;
                        for (var channel = 0; channel < ChannelsPerPixel; channel++)
                            accumulated[channel] += intermediate[offset + channel] * weights[k];
                    }
                    var target = y * result.RowBytes + x * ChannelsPerPixel;
                    var alpha = accumulated[AlphaChannel];
                    for (var channel = 0; channel < AlphaChannel; channel++)
                        output[target + channel] = ToByte(alpha > 0 ? accumulated[channel] * MaximumChannelValue / alpha : 0);
                    output[target + AlphaChannel] = ToByte(alpha);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        } catch {
            result?.Dispose();
            throw;
        } finally {
            ArrayPool<float>.Shared.Return(intermediate, clearArray: true);
        }
    }

    private static byte ToByte(float value) => (byte) Math.Clamp(MathF.Round(value), 0, MaximumChannelValue);
}
