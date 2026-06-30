using System.Numerics;
using Shiron.Backflow.Port;
using Shiron.Backflow.Samples.Port.Validator;
using Silk.NET.Maths;

namespace Shiron.Backflow.Samples.Port.Builder;

public class Vector3PortBuilder<TComponent>(string name)
    : VectorPortBuilder<Vector3PortBuilder<TComponent>, Vector3D<TComponent>, TComponent>
    where TComponent : unmanaged, INumber<TComponent> {
    public override IPortValidator<Vector3D<TComponent>> CreateValidator() => new Vector3PortValidator<TComponent>(this);

    protected override IInputPort<Vector3D<TComponent>> CreateInput() {
        return new InputPort<Vector3D<TComponent>>(name, default, CreateValidator());
    }
    protected override IOutputPort<Vector3D<TComponent>> CreateOutput() {
        return new OutputPort<Vector3D<TComponent>>(name);
    }
}
