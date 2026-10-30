using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;

namespace Shiron.Backflow.Ext.AspNetCore;

/// <summary>Publishes a finite whitelist from the host's DI-backed node registry during startup.</summary>
public sealed class BackflowCatalogBuilder(NodeRegistry registry) {
    private readonly Dictionary<string, BackflowCatalogEntry> _nodes = new(StringComparer.Ordinal);

    /// <summary>Publish a registered node, creating it through the existing registry activator when necessary.</summary>
    public BackflowCatalogBuilder Add<TNode>(string id) where TNode : AbstractNode {
        return Add(id, registry.Get<TNode>() ?? registry.Register<TNode>());
    }

    /// <summary>Publish an existing registry instance; no node is instantiated from a client request.</summary>
    public BackflowCatalogBuilder Add(string id, AbstractNode node) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(node);
        if (!ReferenceEquals(registry.Get(node.GetType()), node))
            throw new ArgumentException("The node must be the host registry's registered instance.", nameof(node));
        if (node.Ports.Select(port => port.Name).Distinct(StringComparer.Ordinal).Count() != node.Ports.Count)
            throw new ArgumentException("Published node port names must be unique.", nameof(node));
        var ports = node.Ports.Select(port => {
            var array = port as IArrayInputPortMarker;
            return new BackflowPortContract(port.Name, port.PortType,
                node.Inputs.Contains(port) ? BackflowPortDirection.Input : BackflowPortDirection.Output,
                port.IsRequired, array?.MinCount, array?.MaxCount);
        }).ToArray();
        _nodes.Add(id, new(node, null, ports));
        return this;
    }

    /// <summary>Publish a declaration-only node contract with generated configuration and port schemas.</summary>
    /// <remarks>No node is instantiated, no implementation is fabricated, and execution of this entry is rejected.</remarks>
    public BackflowCatalogBuilder AddContract<TConfiguration>(string id, Action<BackflowPortBuilder> configurePorts) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(configurePorts);
        var ports = new BackflowPortBuilder();
        configurePorts(ports);
        _nodes.Add(id, new(null, typeof(TConfiguration), ports.Snapshot()));
        return this;
    }

    internal Dictionary<string, BackflowCatalogEntry> Snapshot() => new(_nodes, StringComparer.Ordinal);
}
