using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class BoolPortBuilder(string name) : BasePortBuilder<BoolPortBuilder, bool> {
    protected override IInputPort<bool> CreateInput() {
        return new InputPort<bool>(name, DefaultValue, new BoolPortValidator(this));
    }
    protected override IOutputPort<bool> CreateOutput() {
        return new OutputPort<bool>(name);
    }
}
