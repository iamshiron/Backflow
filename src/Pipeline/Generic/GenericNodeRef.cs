using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Registry;

namespace Shiron.Lib.Pipeline.Generic;

public sealed class GenericNodeRef {
    internal string ID { get; }
    internal NodeBlueprint Blueprint { get; }
    internal Type?[] TypeArgs { get; }
    internal Dictionary<string, Guid> PortMappings { get; }
    internal AbstractNode? MaterializedNode { get; private set; }

    internal bool IsResolved {
        get {
            foreach (var t in TypeArgs) {
                if (t is null) return false;
            }
            return true;
        }
    }

    internal NodeState State { get; set; } = NodeState.Pending;

    private readonly NodeRegistry _registry;

    internal GenericNodeRef(string id, NodeBlueprint blueprint, Dictionary<string, Guid> portMappings, NodeRegistry registry) {
        ID = id;
        Blueprint = blueprint;
        TypeArgs = new Type?[blueprint.TypeParameters.Length];
        PortMappings = portMappings;
        _registry = registry;
    }

    public IPort Port(string name) {
        if (MaterializedNode != null) {
            var realPort = MaterializedNode.Ports.FirstOrDefault(p => p.Name == name);
            if (realPort != null) return realPort;
        }

        var meta = Blueprint.GetPort(name)
            ?? throw new ArgumentException($"Port '{name}' not found on blueprint '{Blueprint.DisplayName}'.", nameof(name));

        var guid = PortMappings[name];
        return new BlueprintPort(meta.Name, guid, meta.TypeParameterIndex ?? -1, meta.Direction);
    }

    internal void Materialize() {
        if (!IsResolved || MaterializedNode != null) return;
        MaterializedNode = _registry.GetOrCreateConcrete(Blueprint.OpenType, TypeArgs!);
    }
}
