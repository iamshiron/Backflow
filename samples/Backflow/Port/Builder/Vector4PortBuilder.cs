using System.Numerics;
using Shiron.Backflow.Port;
using Shiron.Backflow.Samples.Port.Validator;
using Silk.NET.Maths;

namespace Shiron.Backflow.Samples.Port.Builder;

public class Vector4PortBuilder<TComponent>(string name)
    : VectorPortBuilder<Vector4PortBuilder<TComponent>, Vector4D<TComponent>, TComponent>
    where TComponent : unmanaged, INumber<TComponent> {
    public override IPortValidator<Vector4D<TComponent>> CreateValidator() => new Vector4PortValidator<TComponent>(this);

    protected override IInputPort<Vector4D<TComponent>> CreateInput() {
        return new InputPort<Vector4D<TComponent>>(name, default, CreateValidator());
    }
    protected override IOutputPort<Vector4D<TComponent>> CreateOutput() {
        return new OutputPort<Vector4D<TComponent>>(name);
    }
}
