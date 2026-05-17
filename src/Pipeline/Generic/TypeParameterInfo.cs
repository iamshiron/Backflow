using System.Reflection;

namespace Shiron.Lib.Pipeline.Generic;

/// <summary>Describes a single generic type parameter on an open generic node.</summary>
/// <param name="Name">Parameter name (e.g., "T").</param>
/// <param name="Position">0-based position in the generic parameter list.</param>
/// <param name="Constraints">Interface/base-type constraints.</param>
/// <param name="Attributes">Generic parameter attributes (struct, class, new() constraints).</param>
public sealed record TypeParameterInfo(
    string Name,
    int Position,
    Type[] Constraints,
    GenericParameterAttributes Attributes
);
