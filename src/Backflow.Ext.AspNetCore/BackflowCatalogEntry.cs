using Shiron.Backflow.Node;

namespace Shiron.Backflow.Ext.AspNetCore;

internal sealed record BackflowCatalogEntry(AbstractNode? Node, Type? ConfigurationType, BackflowPortContract[] Ports);
