using System.Numerics;
using Shiron.Backflow.Port;
using Shiron.Backflow.Samples.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Backflow.Samples.Port.Validator;

public class Vector2PortValidator<TComponent>(Vector2PortBuilder<TComponent> builder) : IPortValidator<Vector2D<TComponent>>
    where TComponent : unmanaged, INumber<TComponent> {
    public string? Validate(Vector2D<TComponent> value) {
        if (builder.MinValue.HasValue && value.X < builder.MinValue.Value && value.Y < builder.MinValue.Value)
            return $"Value {value} is less than minimum allowed {builder.MinValue.Value}";
        if (builder.MaxValue.HasValue && value.X > builder.MaxValue.Value && value.Y > builder.MaxValue.Value)
            return $"Value {value} is greater than maximum allowed {builder.MaxValue.Value}";
        return null;
    }
}
