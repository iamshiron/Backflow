using Shiron.Backflow.Port.Base;
using Shiron.Backflow.Port.Builder;

namespace Shiron.Backflow.Port.Validator;

/// <summary>Validates that enum values are defined members of <typeparamref name="T"/>.</summary>
public class EnumPortValidator<T>(EnumPortBuilder<T> builder)
    : BasePortValidator<EnumPortBuilder<T>, T>(builder) where T : struct, Enum {
    private static readonly HashSet<T> DefinedValues = [.. Enum.GetValues<T>()];

    protected override string? ValidateValue(T value) {
        return DefinedValues.Contains(value)
            ? null
            : $"Value '{value}' is not a defined member of enum '{typeof(T).Name}'.";
    }
}
