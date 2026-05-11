using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class EnumPortBuilder<T>(string name) : BasePortBuilder<EnumPortBuilder<T>, T> where T : struct, Enum {
    public override IPortValidator<T> CreateValidator() => new EnumPortValidator<T>(this);

    protected override IInputPort<T> CreateInput() {
        return new InputPort<T>(name, DefaultValue, CreateValidator());
    }
    protected override IOutputPort<T> CreateOutput() {
        return new OutputPort<T>(name);
    }
}
