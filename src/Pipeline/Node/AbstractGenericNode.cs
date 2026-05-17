namespace Shiron.Lib.Pipeline.Node;

/// <summary>
/// Base class for open-generic pipeline nodes. Provides <see cref="GetTypeArguments"/>
/// for retrieving the closed generic type parameters at runtime.
/// </summary>
public abstract class AbstractGenericNode : AbstractNode {
    /// <summary>Return the generic type arguments of this closed-generic node instance.</summary>
    public Type[] GetTypeArguments() => GetType().GetGenericArguments();
}
