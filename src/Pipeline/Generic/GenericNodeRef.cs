using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;

namespace Shiron.Lib.Pipeline.Generic;

/// <summary>
/// Handle to an unresolved generic node in a pipeline graph. Type arguments are inferred
/// from connections and the node is materialized into a concrete <see cref="AbstractNode"/>
/// during <see cref="PipelineBuilder.Build"/>.
/// </summary>
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

    /// <summary>
    /// Get a port reference by name. Returns a <see cref="BlueprintPort"/> if the node is not yet resolved,
    /// or the concrete port from the materialized node if it is.
    /// </summary>
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
