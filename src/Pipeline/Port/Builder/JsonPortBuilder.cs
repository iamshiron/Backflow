using System.Text.Json;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

public class JsonPortBuilder(string name) : BasePortBuilder<JsonPortBuilder, JsonDocument> {
    protected override IInputPort<JsonDocument> CreateInput() {
        return new InputPort<JsonDocument>(name, null, new PassAllPortValidator());
    }
    protected override IOutputPort<JsonDocument> CreateOutput() {
        return new OutputPort<JsonDocument>(name);
    }
}
