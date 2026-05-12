namespace Shiron.Lib.Pipeline.Casting;

public sealed record CastRule(
    Type SourceType,
    Type TargetType,
    TypeCast CastType,
    Func<object, object> Converter
);
