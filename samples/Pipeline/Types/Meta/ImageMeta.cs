namespace Shiron.Lib.Samples.Pipeline.Types.Meta;

public readonly record struct ImageMeta(
    int Width,
    int Height,
    string? Format = null,
    string? PixelFormat = null,
    int? BitsPerPixel = null,
    double? DpiX = null,
    double? DpiY = null,
    bool HasAlpha = false,
    bool IsAnimated = false,
    int? FrameCount = null
);
