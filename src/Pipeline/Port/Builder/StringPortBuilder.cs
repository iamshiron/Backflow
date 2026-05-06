using System.Numerics;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class StringPortBuilder(string name) : BasePortBuilder<StringPortBuilder, string> {
    public int? MaxLengthValue { get; protected set; }
    public int? MinLengthValue { get; protected set; }

    public StringPortBuilder MaxLength(int maxLength) {
        MaxLengthValue = maxLength;
        return this;
    }
    public StringPortBuilder MinLength(int minLength) {
        MinLengthValue = minLength;
        return this;
    }
    public StringPortBuilder Range(int minLength, int maxLength) {
        return MinLength(minLength).MaxLength(maxLength);
    }

    protected override IInputPort<string> CreateInput() {
        return new InputPort<string>(name, DefaultValue, new StringPortValidator(this));
    }
    protected override IOutputPort<string> CreateOutput() {
        return new OutputPort<string>(name);
    }
}
