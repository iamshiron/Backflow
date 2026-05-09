using System.Numerics;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Samples.Pipeline.Port.Validator;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Port.Builder;

public class Vector2PortBuilder<TComponent>(string name)
    : VectorPortBuilder<Vector2PortBuilder<TComponent>, Vector2D<TComponent>, TComponent>
    where TComponent : unmanaged, INumber<TComponent> {
    protected override IInputPort<Vector2D<TComponent>> CreateInput() {
        return new InputPort<Vector2D<TComponent>>(name, default, new Vector2PortValidator<TComponent>());
    }
    protected override IOutputPort<Vector2D<TComponent>> CreateOutput() {
        return new OutputPort<Vector2D<TComponent>>(name);
    }
}
