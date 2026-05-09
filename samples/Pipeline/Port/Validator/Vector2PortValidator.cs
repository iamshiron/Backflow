using System.Numerics;
using Shiron.Lib.Pipeline.Port;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Port.Validator;

public class Vector2PortValidator<TComponent> : IPortValidator<Vector2D<TComponent>>
    where TComponent : unmanaged, INumber<TComponent> {
    public string? Validate(Vector2D<TComponent> value) {
        return null;
    }
}
