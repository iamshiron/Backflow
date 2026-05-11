using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class ArrayPortBuilder<T>(string name) : BasePortBuilder<ArrayPortBuilder<T>, T[]> {
    public int? MinLengthValue { get; protected set; }
    public int? MaxLengthValue { get; protected set; }

    public ArrayPortBuilder<T> MinLength(int minLength) {
        MinLengthValue = minLength;
        return this;
    }
    public ArrayPortBuilder<T> MaxLength(int maxLength) {
        MaxLengthValue = maxLength;
        return this;
    }
    public ArrayPortBuilder<T> Range(int minLength, int maxLength) {
        return MinLength(minLength).MaxLength(maxLength);
    }

    public override IPortValidator<T[]> CreateValidator() => new ArrayPortValidator<T>(this);

    protected override IInputPort<T[]> CreateInput() {
        return new InputPort<T[]>(name, [], CreateValidator());
    }
    protected override IOutputPort<T[]> CreateOutput() {
        return new OutputPort<T[]>(name);
    }
}
