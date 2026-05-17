using Shiron.Lib.Pipeline.Generic;

namespace Shiron.Lib.Pipeline.Exceptions;

/// <summary>Thrown when a resolved type argument violates the generic constraint on the type parameter.</summary>
public class GenericConstraintException(
    string nodeId,
    TypeParameterInfo typeParam,
    Type inferredType,
    string reason
) : Exception(
    $"Cannot resolve type parameter '{typeParam.Name}' of node '{nodeId}' as '{inferredType.Name}': {reason}."
) {
    public string NodeId { get; } = nodeId;
    public string TypeParameterName { get; } = typeParam.Name;
    public Type InferredType { get; } = inferredType;
    public string Reason { get; } = reason;
}
