namespace Shiron.Lib.Pipeline.Exceptions;

public class TypeIncompatibilityException(
    string sourcePortName,
    Type sourceType,
    string targetPortName,
    Type targetType
) : Exception(
    $"Cannot connect port '{sourcePortName}' ({sourceType.Name}) to port '{targetPortName}' ({targetType.Name}): " +
    $"type '{sourceType.Name}' is not compatible with '{targetType.Name}'."
) {
    public string SourcePortName { get; } = sourcePortName;
    public Type SourceType { get; } = sourceType;
    public string TargetPortName { get; } = targetPortName;
    public Type TargetType { get; } = targetType;
}
