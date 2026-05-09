using System.Numerics;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Samples.Pipeline.Port.Validator;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Port.Builder;

public class Vector3PortBuilder<TComponent>(string name)
    : VectorPortBuilder<Vector3PortBuilder<TComponent>, Vector3D<TComponent>, TComponent>
    where TComponent : unmanaged, INumber<TComponent> {
    protected override IInputPort<Vector3D<TComponent>> CreateInput() {
        return new InputPort<Vector3D<TComponent>>(name, default, new Vector3PortValidator<TComponent>(this));
    }
    protected override IOutputPort<Vector3D<TComponent>> CreateOutput() {
        return new OutputPort<Vector3D<TComponent>>(name);
    }
}
