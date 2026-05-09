using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Pipeline.Port.Validator;
using Shiron.Lib.Samples.Pipeline.Types;

namespace Shiron.Lib.Samples.Pipeline.Port.Builder;

public class BlobPortBuilder<TValue>(string name) : BasePortBuilder<BlobPortBuilder<TValue>, TValue> where TValue : class, IBlob {
    protected override IInputPort<TValue> CreateInput() {
        return new InputPort<TValue>(name, null, new PassAllPortValidator());
    }
    protected override IOutputPort<TValue> CreateOutput() {
        return new OutputPort<TValue>(name);
    }
}
