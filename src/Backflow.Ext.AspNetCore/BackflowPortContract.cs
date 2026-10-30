namespace Shiron.Backflow.Ext.AspNetCore;

internal sealed record BackflowPortContract(string Name, Type Type, BackflowPortDirection Direction, bool Required,
    int? MinimumCount, int? MaximumCount);
