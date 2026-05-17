using System.Reflection;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Generic;

/// <summary>
/// Creates <see cref="NodeBlueprint"/> instances from open generic types via reflection.
/// Extracts port metadata from public properties typed as <see cref="IInputPort{T}"/> or <see cref="IOutputPort{T}"/>.
/// </summary>
public static class BlueprintFactory {
    /// <summary>Build a blueprint by reflecting over the open generic type's ports and type parameters.</summary>
    public static NodeBlueprint FromOpenType(Type openType) {
        if (!openType.IsGenericTypeDefinition)
            throw new ArgumentException($"Type {openType} is not an open generic type definition.", nameof(openType));

        var genericParams = openType.GetGenericArguments();
        var typeParams = new TypeParameterInfo[genericParams.Length];
        for (var i = 0; i < genericParams.Length; i++) {
            var gp = genericParams[i];
            typeParams[i] = new TypeParameterInfo(
                gp.Name,
                gp.GenericParameterPosition,
                gp.GetGenericParameterConstraints(),
                gp.GenericParameterAttributes
            );
        }

        var ports = new List<PortTypeMetadata>();
        foreach (var prop in openType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            var propType = prop.PropertyType;
            if (!propType.IsGenericType) continue;

            var genericDef = propType.GetGenericTypeDefinition();
            var isInput = genericDef == typeof(IInputPort<>);
            var isOutput = genericDef == typeof(IOutputPort<>);
            if (!isInput && !isOutput) continue;

            var typeArg = propType.GetGenericArguments()[0];

            int? typeParamIndex = null;
            Type? concreteType = null;
            if (typeArg.IsGenericParameter) {
                typeParamIndex = typeArg.GenericParameterPosition;
            } else {
                concreteType = typeArg;
            }

            ports.Add(new PortTypeMetadata(
                prop.Name,
                isInput ? PortDirection.Input : PortDirection.Output,
                typeParamIndex,
                concreteType
            ));
        }

        var displayName = openType.Name;
        var backtickIdx = displayName.IndexOf('`');
        if (backtickIdx > 0) displayName = displayName[..backtickIdx];

        return new NodeBlueprint(openType, displayName, typeParams, [.. ports]);
    }
}
