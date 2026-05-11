using System.Numerics;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Base;
using Shiron.Lib.Samples.Pipeline.Port.Validator;
using Silk.NET.Maths;

namespace Shiron.Lib.Samples.Pipeline.Port.Builder;

public abstract class VectorPortBuilder<TBuilder, TVector, TComponent>
    : BasePortBuilder<TBuilder, TVector>
    where TBuilder : VectorPortBuilder<TBuilder, TVector, TComponent>
    where TComponent : unmanaged, INumber<TComponent> {
    public TComponent? MinValue { get; protected set; }
    public TComponent? MaxValue { get; protected set; }

    public TBuilder Min(TComponent? min) {
        MinValue = min;
        return (TBuilder) this;
    }

    public TBuilder Max(TComponent? max) {
        MaxValue = max;
        return (TBuilder) this;
    }

    public TBuilder Range(TComponent? min, TComponent? max) {
        return Min(min).Max(max);
    }
}
