using System.Numerics;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Base;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Port.Builder;

// TVector represents the full vector (e.g., Vector3D<float>)
// TComponent represents the underlying number type (e.g., float)
public abstract class VectorPortBuilder<TBuilder, TVector, TComponent>
    : BasePortBuilder<TBuilder, TVector>
    where TBuilder : VectorPortBuilder<TBuilder, TVector, TComponent>
    where TComponent : unmanaged, INumber<TComponent> {
    public TComponent? MinComponentValue { get; protected set; }
    public TComponent? MaxComponentValue { get; protected set; }

    public TBuilder Min(TComponent? min) {
        MinComponentValue = min;
        return (TBuilder) this;
    }

    public TBuilder Max(TComponent? max) {
        MaxComponentValue = max;
        return (TBuilder) this;
    }

    public TBuilder Range(TComponent? min, TComponent? max) {
        return Min(min).Max(max);
    }
}
