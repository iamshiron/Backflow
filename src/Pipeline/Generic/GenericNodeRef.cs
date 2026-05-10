using Shiron.Lib.Pipeline.Node;

namespace Shiron.Lib.Pipeline.Generic;

public sealed class GenericNodeRef {
    internal string ID { get; }
    internal NodeBlueprint Blueprint { get; }
    internal Type?[] TypeArgs { get; }
    internal Dictionary<string, Guid> PortMappings { get; }
    internal bool IsResolved {
        get {
            foreach (var t in TypeArgs) {
                if (t is null) return false;
            }
            return true;
        }
    }

    internal NodeState State { get; set; } = NodeState.Pending;

    internal GenericNodeRef(string id, NodeBlueprint blueprint, Dictionary<string, Guid> portMappings) {
        ID = id;
        Blueprint = blueprint;
        TypeArgs = new Type?[blueprint.TypeParameters.Length];
        PortMappings = portMappings;
    }

    public BlueprintPort Port(string name) {
        var meta = Blueprint.GetPort(name)
            ?? throw new ArgumentException($"Port '{name}' not found on blueprint '{Blueprint.DisplayName}'.", nameof(name));

        var guid = PortMappings[name];
        return new BlueprintPort(meta.Name, guid, meta.TypeParameterIndex ?? -1, meta.Direction);
    }
}
