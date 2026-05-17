namespace Shiron.Lib.Pipeline.Casting;

/// <summary>
/// Registry of type-conversion rules used during port connection validation and context reads.
/// Ships with built-in widening/narrowing numeric casts and an implicit <c>ToString</c> fallback.
/// </summary>
public class CastRegistry {
    private readonly Dictionary<(Type source, Type target), ICastRule> _rules = [];
    private readonly Dictionary<Type, ToStringCastRule> _toStringCache = [];

    /// <summary>Singleton default registry with builtin numeric casts pre-registered.</summary>
    public static CastRegistry Default { get; } = CreateDefault();

    /// <summary>Create a registry with all builtin numeric widening and narrowing casts.</summary>
    public static CastRegistry CreateDefault() {
        var registry = new CastRegistry();
        BuiltinNumericCasts.RegisterAll(registry);
        return registry;
    }

    /// <summary>Register a typed conversion rule.</summary>
    public CastRegistry Register<TSrc, TDst>(TypeCast castType, Func<TSrc, TDst> converter) {
        var key = (typeof(TSrc), typeof(TDst));
        _rules[key] = new CastRule<TSrc, TDst>(
            typeof(TSrc),
            typeof(TDst),
            castType,
            obj => converter((TSrc) obj!)
        );
        return this;
    }

    /// <summary>Register a generic <see cref="ICastRule"/>.</summary>
    public CastRegistry Register(ICastRule rule) {
        _rules[(rule.SourceType, rule.TargetType)] = rule;
        return this;
    }

    /// <summary>Cast a value using the registered rule. Throws if no rule exists.</summary>
    public TDst? Cast<TSrc, TDst>(TSrc? value) {
        if (value is null) return default;
        var rule = (CastRule<TSrc, TDst>?) _rules[(typeof(TSrc), typeof(TDst))];
        if (rule is null) throw new InvalidOperationException($"No cast rule registered for {typeof(TSrc)} -> {typeof(TDst)}");
        return (TDst?) rule.Cast(value);
    }

    /// <summary>Try to find a cast rule between two types. Falls back to <c>ToString</c> if target is <c>string</c>.</summary>
    public bool TryGetCast(Type source, Type target, out ICastRule? rule) {
        if (_rules.TryGetValue((source, target), out rule))
            return true;

        if (target == typeof(string) && source != typeof(string)) {
            rule = GetToStringRule(source);
            return true;
        }

        return false;
    }

    /// <summary>Get the <see cref="TypeCast"/> classification for a source-target pair.</summary>
    public TypeCast GetCastType(Type source, Type target) {
        return TryGetCast(source, target, out var rule) ? rule!.CastType : TypeCast.None;
    }

    /// <summary>Whether a cast exists between two types (including <c>ToString</c> fallback).</summary>
    public bool CanCast(Type source, Type target) {
        if (source == target) return true;
        if (_rules.ContainsKey((source, target))) return true;
        return target == typeof(string) && source != typeof(string);
    }

    private ToStringCastRule GetToStringRule(Type source) {
        if (!_toStringCache.TryGetValue(source, out var rule)) {
            rule = new ToStringCastRule(source);
            _toStringCache[source] = rule;
        }
        return rule;
    }
}
