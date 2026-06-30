using System.Text.Json;
using Shiron.Backflow.Port.Base;
using Shiron.Backflow.Port.Validator;

namespace Shiron.Backflow.Port.Builder;

/// <summary>Fluent builder for <see cref="JsonElement"/> ports.</summary>
public class JsonElementPortBuilder(string name) : BasePortBuilder<JsonElementPortBuilder, JsonElement> {
    public override IPortValidator<JsonElement> CreateValidator() => new PassAllPortValidator<JsonElement>();

    protected override IInputPort<JsonElement> CreateInput() {
        return new InputPort<JsonElement>(name, default, CreateValidator());
    }
    protected override IOutputPort<JsonElement> CreateOutput() {
        return new OutputPort<JsonElement>(name);
    }
}
