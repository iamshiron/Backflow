namespace Shiron.Lib.Pipeline.Caching;

public interface ICacheKey {
    string NodeType { get; }
    string AssemblyVersion { get; }
    string InputHash { get; }
    string CombinedHash { get; }
}
