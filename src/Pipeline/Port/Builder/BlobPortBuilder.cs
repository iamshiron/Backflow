using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline.Port.Builder;

/// <summary>Fluent builder for blob ports (<see cref="IBlob"/> subtypes).</summary>
public class BlobPortBuilder<TValue>(string name) : BasePortBuilder<BlobPortBuilder<TValue>, TValue> where TValue : class, IBlob {
    public override IPortValidator<TValue> CreateValidator() => new PassAllPortValidator<TValue>();

    protected override IInputPort<TValue> CreateInput() {
        return new InputPort<TValue>(name, null, CreateValidator());
    }
    protected override IOutputPort<TValue> CreateOutput() {
        return new OutputPort<TValue>(name);
    }
}
