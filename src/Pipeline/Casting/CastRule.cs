namespace Shiron.Lib.Pipeline.Casting;

/// <summary>
/// Typed cast rule carrying a conversion delegate and a <see cref="TypeCast"/> classification.
/// </summary>
public sealed class CastRule<TSrc, TDst>(Type source, Type target, TypeCast type, Func<TSrc?, TDst?> converter) : ICastRule {
    public Type SourceType { get; } = source;
    public Type TargetType { get; } = target;
    /// <summary>Whether this conversion is lossless or lossy.</summary>
    public TypeCast CastType { get; } = type;

    /// <summary>Apply the conversion to a boxed value.</summary>
    public object? Cast(object? value) {
        return converter((TSrc?) value);
    }
}

/// <summary>Fallback cast rule that calls <see cref="object.ToString"/> on the source value.</summary>
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

/// <summary>Untyped cast rule interface used by <see cref="CastRegistry"/>.</summary>
public interface ICastRule {
    /// <summary>Source type of the conversion.</summary>
    Type SourceType { get; }
    /// <summary>Target type of the conversion.</summary>
    Type TargetType { get; }
    /// <summary>Classification of the conversion.</summary>
    TypeCast CastType { get; }

    /// <summary>Apply the conversion to a boxed value.</summary>
    object? Cast(object? value);
}
