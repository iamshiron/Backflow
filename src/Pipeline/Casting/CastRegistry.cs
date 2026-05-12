namespace Shiron.Lib.Pipeline.Casting;

public class CastRegistry {
    private readonly Dictionary<(Type Source, Type Target), CastRule> _rules = [];

    public static CastRegistry Default { get; } = CreateDefault();

    public static CastRegistry CreateDefault() {
        var registry = new CastRegistry();
        BuiltinNumericCasts.RegisterAll(registry);
        return registry;
    }

    public CastRegistry Register<TSrc, TDst>(TypeCast castType, Func<TSrc, TDst> converter) {
        var key = (typeof(TSrc), typeof(TDst));
        _rules[key] = new CastRule(
            typeof(TSrc),
            typeof(TDst),
            castType,
            obj => converter((TSrc) obj)!
        );
        return this;
    }

    public CastRegistry Register(CastRule rule) {
        _rules[(rule.SourceType, rule.TargetType)] = rule;
        return this;
    }

    public bool TryGetCast(Type source, Type target, out CastRule? rule) {
        return _rules.TryGetValue((source, target), out rule);
    }

    public TypeCast GetCastType(Type source, Type target) {
        return TryGetCast(source, target, out var rule) ? rule!.CastType : TypeCast.None;
    }

    public bool CanCast(Type source, Type target) {
        return source == target || _rules.ContainsKey((source, target));
    }
}
