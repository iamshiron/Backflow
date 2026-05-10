using System.Reflection;

namespace Shiron.Lib.Pipeline.Generic;

public sealed record TypeParameterInfo(
    string Name,
    int Position,
    Type[] Constraints,
    GenericParameterAttributes Attributes
);
