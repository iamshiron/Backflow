namespace Shiron.Lib.Pipeline.Node;

[Flags]
public enum NodeState : byte {
    None = 0,
    Pending = 1 << 0,
    Executing = 1 << 1,
    Done = 1 << 2,
    Failed = 1 << 3,
    Skipped = 1 << 4,
}
