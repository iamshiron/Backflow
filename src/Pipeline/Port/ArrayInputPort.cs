using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;

namespace Shiron.Lib.Pipeline.Port;

/// <summary>
/// Concrete <see cref="IArrayInputPort{T}"/> implementation with frozen-count assembly.
/// Elements are assembled from indexed connections during pipeline execution.
/// </summary>
public class ArrayInputPort<T>(
    string name,
    T? elementDefault,
    IPortValidator<T> elementValidator,
    IPortValidator<T[]> arrayValidator,
    int minCount,
    int? maxCount
) : Port(name), IArrayInputPort<T>, IArrayPortAssembly {
    public override Type PortType { get; protected set; } = typeof(T[]);
    public Type ElementType => typeof(T);
    public int MinCount { get; } = minCount;
    public int? MaxCount { get; } = maxCount;

    internal T? ElementDefault => elementDefault;

    private int? _count;
    public int? Count => _count;
    public bool IsFrozen => _count.HasValue;

    public void SetCount(int count) {
        if (IsFrozen)
            throw new InvalidOperationException(
                $"Count for port '{Name}' is already frozen at {_count}.");

        if (count < MinCount)
            throw new ArgumentException(
                $"Count {count} is less than minimum {MinCount}.", nameof(count));

        if (MaxCount.HasValue && count > MaxCount.Value)
            throw new ArgumentException(
                $"Count {count} exceeds maximum {MaxCount.Value}.", nameof(count));

        _count = count;
    }

    public T[]? Read(INodeContext context) {
        var value = context.Has<T[]>(this)
            ? context.Read<T[]>(this)
            : IsFrozen
                ? CreateDefaultArray()
                : null;
        FailFast(value);
        return value;
    }

    public object? ReadAny(INodeContext context) {
        var value = Read(context);
        return value;
    }

    public bool TryRead(INodeContext context, out T[]? value) {
        var has = context.Has<T[]>(this);
        if (!has) {
            value = IsFrozen ? CreateDefaultArray() : null;
            return false;
        }

        value = context.Read<T[]>(this);
        FailFast(value);
        return true;
    }

    public bool HasValue(INodeContext context) {
        return context.Has<T[]>(this);
    }

    public T? ReadAt(INodeContext context, int index) {
        var array = Read(context);
        if (array is null || index < 0 || index >= array.Length)
            return elementDefault;
        return array[index];
    }

    public bool HasValueAt(INodeContext context, int index) {
        var array = Read(context);
        return array is not null && index >= 0 && index < array.Length;
    }

    public int GetCount(INodeContext context) {
        if (IsFrozen) return _count!.Value;
        var array = Read(context);
        return array?.Length ?? 0;
    }

    void IArrayPortAssembly.Assemble(IPipelineContext context, Guid targetGuid, IReadOnlyList<(int Index, Guid SourceGuid)> sources) {
        if (!IsFrozen)
            throw new InvalidOperationException($"Cannot assemble array port '{Name}' without a frozen count.");

        var count = _count!.Value;
        var array = new T[count];
        if (elementDefault is T def) Array.Fill(array, def);

        foreach (var (index, sourceGuid) in sources) {
            if (index >= 0 && index < count) {
                array[index] = context.Read<T>(sourceGuid) ?? (elementDefault is T d ? d : default!);
            }
        }

        for (var i = 0; i < array.Length; i++) {
            var error = elementValidator.Validate(array[i]);
            if (error is not null)
                throw new PortValidationException($"{Name}[{i}]", array[i], error);
        }

        context.Write(targetGuid, array);
    }

    void IArrayPortAssembly.AssembleWithCount(IPipelineContext context, Guid targetGuid, IReadOnlyList<(int Index, Guid SourceGuid)> sources, int count) {
        SetCount(count);
        ((IArrayPortAssembly) this).Assemble(context, targetGuid, sources);
    }

    private T[] CreateDefaultArray() {
        var count = _count!.Value;
        var array = new T[count];
        if (elementDefault is T def) Array.Fill(array, def);
        return array;
    }

    private void FailFast(T[]? value) {
        var error = arrayValidator.Validate(value);
        if (error is not null)
            throw new PortValidationException(Name, value, error);

        if (value is null) return;

        for (var i = 0; i < value.Length; i++) {
            error = elementValidator.Validate(value[i]);
            if (error is not null)
                throw new PortValidationException($"{Name}[{i}]", value[i], error);
        }
    }
}
