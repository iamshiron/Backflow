namespace Shiron.Lib.Pipeline.Port;

public interface IPortGroup : IPort {
    int MinCount { get; }
    int? MaxCount { get; }
    Type ElementType { get; }
}
