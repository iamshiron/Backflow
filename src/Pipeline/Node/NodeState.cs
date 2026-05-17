namespace Shiron.Lib.Pipeline.Node;

/// <summary>Lifecycle state of a pipeline node during and after execution.</summary>
[Flags]
public enum NodeState : byte {
    None = 0,
    /// <summary>Not yet executed.</summary>
    Pending = 1 << 0,
    /// <summary>Currently executing.</summary>
    Executing = 1 << 1,
    /// <summary>Successfully executed.</summary>
    Done = 1 << 2,
    /// <summary>Execution returned <c>false</c> or threw an exception.</summary>
    Failed = 1 << 3,
    /// <summary>Skipped due to upstream propagation or behavior suppression.</summary>
    Skipped = 1 << 4,
}
