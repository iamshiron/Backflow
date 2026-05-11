using System.Text.Json;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class JsonElementPortBuilder(string name) : BasePortBuilder<JsonElementPortBuilder, JsonElement> {
    public override IPortValidator<JsonElement> CreateValidator() => new PassAllPortValidator<JsonElement>();

    protected override IInputPort<JsonElement> CreateInput() {
        return new InputPort<JsonElement>(name, default, CreateValidator());
    }
    protected override IOutputPort<JsonElement> CreateOutput() {
        return new OutputPort<JsonElement>(name);
    }
}
