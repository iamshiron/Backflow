using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class EnumPortBuilder<T>(string name) : BasePortBuilder<EnumPortBuilder<T>, T> where T : struct, Enum {
    protected override IInputPort<T> CreateInput() {
        return new InputPort<T>(name, DefaultValue, new EnumPortValidator<T>(this));
    }
    protected override IOutputPort<T> CreateOutput() {
        return new OutputPort<T>(name);
    }
}
