using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>
/// Fluent builder for <c>string</c> ports with length and multiline constraints.
/// </summary>
public class StringPortBuilder(string name) : BasePortBuilder<StringPortBuilder, string> {
    /// <summary>Maximum string length, or <c>null</c> for no limit.</summary>
    public int? MaxLengthValue { get; protected set; }
    /// <summary>Minimum string length, or <c>null</c> for no limit.</summary>
    public int? MinLengthValue { get; protected set; }
    /// <summary>Whether newline characters are allowed.</summary>
    public bool? AllowMultiline { get; protected set; }

    /// <summary>Set the maximum string length.</summary>
    public StringPortBuilder MaxLength(int maxLength) {
        MaxLengthValue = maxLength;
        return this;
    }
    /// <summary>Set the minimum string length.</summary>
    public StringPortBuilder MinLength(int minLength) {
        MinLengthValue = minLength;
        return this;
    }
    /// <summary>Set both minimum and maximum string length.</summary>
    public StringPortBuilder Range(int minLength, int maxLength) {
        return MinLength(minLength).MaxLength(maxLength);
    }
    /// <summary>Allow or disallow newline characters.</summary>
    public StringPortBuilder Multiline(bool allowMultiline = true) {
        AllowMultiline = allowMultiline;
        return this;
    }

    public override IPortValidator<string> CreateValidator() => new StringPortValidator(this);

    protected override IInputPort<string> CreateInput() {
        return new InputPort<string>(name, DefaultValue, CreateValidator());
    }
    protected override IOutputPort<string> CreateOutput() {
        return new OutputPort<string>(name);
    }
}
