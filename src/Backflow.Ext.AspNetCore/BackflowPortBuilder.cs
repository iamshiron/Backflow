namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>Declares typed conceptual ports without constructing an executable node.</summary>
public sealed class BackflowPortBuilder {
    private readonly List<BackflowPortContract> _ports = [];

    /// <summary>Declare an input whose schema is generated from <typeparamref name="T"/>.</summary>
    public BackflowPortBuilder Input<T>(string name, bool required = true) =>
        Add(new(name, typeof(T), BackflowPortDirection.Input, required, null, null));

    /// <summary>Declare an indexed array input with cardinality bounds.</summary>
    public BackflowPortBuilder ArrayInput<T>(string name, int minimumCount = 0, int? maximumCount = null, bool required = true) {
        if (minimumCount < 0 || maximumCount < minimumCount)
            throw new ArgumentOutOfRangeException(nameof(minimumCount));
        return Add(new(name, typeof(T[]), BackflowPortDirection.Input, required, minimumCount, maximumCount));
    }

    /// <summary>Declare an output whose schema is generated from <typeparamref name="T"/>.</summary>
    public BackflowPortBuilder Output<T>(string name) =>
        Add(new(name, typeof(T), BackflowPortDirection.Output, true, null, null));

    internal BackflowPortContract[] Snapshot() => _ports.ToArray();

    private BackflowPortBuilder Add(BackflowPortContract port) {
        ArgumentException.ThrowIfNullOrWhiteSpace(port.Name);
        if (_ports.Any(existing => existing.Name == port.Name))
            throw new ArgumentException("Port names must be unique.", nameof(port));
        _ports.Add(port);
        return this;
    }
}
