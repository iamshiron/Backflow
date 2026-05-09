using System.Numerics;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Samples.Pipeline.Port.Builder;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Port.Validator;

public class Vector4PortValidator<TComponent>(Vector4PortBuilder<TComponent> builder) : IPortValidator<Vector4D<TComponent>>
    where TComponent : unmanaged, INumber<TComponent> {
    public string? Validate(Vector4D<TComponent> value) {
        if (builder.MinValue.HasValue && value.X < builder.MinValue.Value && value.Y < builder.MinValue.Value && value.Z < builder.MinValue.Value && value.W < builder.MinValue.Value)
            return $"Value {value} is less than minimum allowed {builder.MinValue.Value}";
        if (builder.MaxValue.HasValue && value.X > builder.MaxValue.Value && value.Y > builder.MaxValue.Value && value.Z > builder.MaxValue.Value && value.W > builder.MaxValue.Value)
            return $"Value {value} is greater than maximum allowed {builder.MaxValue.Value}";
        return null;
    }
}
