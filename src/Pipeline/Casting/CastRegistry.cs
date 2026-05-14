namespace Shiron.Lib.Pipeline.Casting;

public class CastRegistry {
    private readonly Dictionary<(Type source, Type target), ICastRule> _rules = [];
    private readonly Dictionary<Type, ToStringCastRule> _toStringCache = [];

    public static CastRegistry Default { get; } = CreateDefault();

    public static CastRegistry CreateDefault() {
        var registry = new CastRegistry();
        BuiltinNumericCasts.RegisterAll(registry);
        return registry;
    }

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

    public CastRegistry Register(ICastRule rule) {
        _rules[(rule.SourceType, rule.TargetType)] = rule;
        return this;
    }

    public TDst? Cast<TSrc, TDst>(TSrc? value) {
        if (value is null) return default;
        var rule = (CastRule<TSrc, TDst>?) _rules[(typeof(TSrc), typeof(TDst))];
        if (rule is null) throw new InvalidOperationException($"No cast rule registered for {typeof(TSrc)} -> {typeof(TDst)}");
        return (TDst?) rule.Cast(value);
    }

    public bool TryGetCast(Type source, Type target, out ICastRule? rule) {
        if (_rules.TryGetValue((source, target), out rule))
            return true;

        if (target == typeof(string) && source != typeof(string)) {
            rule = GetToStringRule(source);
            return true;
        }

        return false;
    }

    public TypeCast GetCastType(Type source, Type target) {
        return TryGetCast(source, target, out var rule) ? rule!.CastType : TypeCast.None;
    }

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
