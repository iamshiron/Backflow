using System.Text.Json;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;

namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>Fluent builder for <see cref="JsonDocument"/> ports.</summary>
public class JsonDocumentPortBuilder(string name) : BasePortBuilder<JsonDocumentPortBuilder, JsonDocument> {
    public override IPortValidator<JsonDocument> CreateValidator() => new PassAllPortValidator<JsonDocument>();

    protected override IInputPort<JsonDocument> CreateInput() {
        return new InputPort<JsonDocument>(name, null, CreateValidator());
    }
    protected override IOutputPort<JsonDocument> CreateOutput() {
        return new OutputPort<JsonDocument>(name);
    }
}
