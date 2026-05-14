namespace Shiron.Lib.Pipeline.Casting;

public sealed class CastRule<TSrc, TDst>(Type source, Type target, TypeCast type, Func<TSrc?, TDst?> converter) : ICastRule {
    public Type SourceType { get; } = source;
    public Type TargetType { get; } = target;
    public TypeCast CastType { get; } = type;

    public object? Cast(object? value) {
        return converter((TSrc?) value);
    }
}

public sealed class ToStringCastRule : ICastRule {
    public Type SourceType { get; }
    public Type TargetType { get; } = typeof(string);
    public TypeCast CastType { get; } = TypeCast.Lossless;

    public ToStringCastRule(Type sourceType) {
        SourceType = sourceType;
    }

    public object? Cast(object? value) {
        return value?.ToString();
    }
}

public interface ICastRule {
    Type SourceType { get; }
    Type TargetType { get; }
    TypeCast CastType { get; }

    object? Cast(object? value);
}
