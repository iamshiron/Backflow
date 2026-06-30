using System.Text;
using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Events;
using Shiron.Backflow.Node;
using Shiron.Backflow.Types;
using Spectre.Console;

namespace Shiron.Backflow.Samples.Listeners;

/// <summary>
/// A <see cref="IPipelineEventListener"/> that pretty-prints pipeline execution to the console using Spectre.Console,
/// rendering one table per layer (status, node, duration, and output port values) plus a run summary.
/// Thread-safe: node events fire concurrently under <see cref="PipelineExecutor.ExecuteAsync"/>.
/// </summary>
public sealed class ConsolePipelineEventListener(IPipelineContext context) : IPipelineEventListener {
    private readonly object _lock = new();

    private IReadOnlyList<PipelineBuilder.NodeInstance> _currentLayerNodes = [];
    private readonly Dictionary<string, NodeResult> _results = [];

    /// <inheritdoc/>
    public void OnPipelineStart(in PipelineStartEvent e) {
        lock (_lock) {
            AnsiConsole.Write(new Rule("[bold blue]Pipeline Start[/]") { Justification = Justify.Left });
            AnsiConsole.MarkupLine(
                "  [grey]Layers:[/] [bold]{0}[/]   [grey]Nodes:[/] [bold]{1}[/]",
                e.TotalLayers, e.TotalNodes
            );
        }
    }

    /// <inheritdoc/>
    public void OnLayerStart(in PipelineLayerStartEvent e) {
        lock (_lock) {
            _currentLayerNodes = e.Nodes;
            _results.Clear();
        }
    }

    /// <inheritdoc/>
    public void OnNodeStart(in PipelineNodeStartEvent e) { }

    /// <inheritdoc/>
    public void OnNodeSkip(in PipelineNodeSkipEvent e) {
        lock (_lock) {
            _results[e.Node.ID] = new NodeResult(NodeState.Skipped, TimeSpan.Zero, FromCache: false, Error: null);
        }
    }

    /// <inheritdoc/>
    public void OnNodeSuccess(in PipelineNodeSuccessEvent e) {
        lock (_lock) {
            _results[e.Node.ID] = new NodeResult(NodeState.Done, e.Elapsed, e.FromCache, Error: null);
        }
    }

    /// <inheritdoc/>
    public void OnNodeFailure(in PipelineNodeFailureEvent e) {
        lock (_lock) {
            _results[e.Node.ID] = new NodeResult(NodeState.Failed, e.Elapsed, FromCache: false, e.Exception);
        }
    }

    /// <inheritdoc/>
    public void OnLayerComplete(in PipelineLayerCompleteEvent e) {
        lock (_lock) {
            var table = new Table { Border = TableBorder.Rounded };
            table.Title = new TableTitle(
                $"Layer {e.LayerIndex}  [grey]({e.NodeCount} nodes, {FormatDuration(e.Elapsed)})[/]"
            );
            table.AddColumn(new TableColumn("Status"));
            table.AddColumn(new TableColumn("Node"));
            table.AddColumn(new TableColumn("ID"));
            table.AddColumn(new TableColumn("Duration"));
            table.AddColumn(new TableColumn("Outputs"));

            foreach (var node in _currentLayerNodes) {
                if (!_results.TryGetValue(node.ID, out var result)) continue;

                var (icon, color) = StatusDisplay(result);
                table.AddRow(
                    $"[{color}]{icon}[/]",
                    Markup.Escape(Simplify(node.Node.GetType().Name)),
                    $"[grey]{Markup.Escape(node.ID)}[/]",
                    FormatDurationColumn(result),
                    FormatDetail(node, result)
                );
            }

            AnsiConsole.Write(table);
            _results.Clear();
            _currentLayerNodes = [];
        }
    }

    /// <inheritdoc/>
    public void OnPipelineComplete(in PipelineCompleteEvent e) {
        lock (_lock) {
            AnsiConsole.Write(new Rule("[bold green]Pipeline Complete[/]") { Justification = Justify.Left });
            var s = e.Stats;
            var grid = new Grid().AddColumn().AddColumn();
            grid.AddRow("[grey]Total nodes:[/]", $"{s.TotalNodes}");
            grid.AddRow("[grey]Executed:[/]", $"[green]{s.ExecutedNodes}[/]");
            grid.AddRow("[grey]Skipped:[/]", $"[yellow]{s.SkippedNodes}[/]");
            grid.AddRow("[grey]Cache hits:[/]", $"[cyan]{s.CacheHits}[/]");
            grid.AddRow("[grey]Cache misses:[/]", $"{s.CacheMisses}");
            grid.AddRow("[grey]Total time:[/]", FormatDuration(s.TotalTime));
            AnsiConsole.Write(grid);
        }
    }

    private string FormatDetail(PipelineBuilder.NodeInstance node, NodeResult result) {
        if (result.State == NodeState.Failed) {
            var msg = result.Error?.Message ?? "unknown error";
            return $"[red]{Markup.Escape(msg)}[/]";
        }
        if (result.State == NodeState.Skipped) return "[dim]skipped[/]";
        return FormatOutputs(node);
    }

    private string FormatOutputs(PipelineBuilder.NodeInstance node) {
        var outputs = node.Node.Outputs;
        if (outputs.Count == 0) return "[dim]—[/]";

        var parts = new List<string>(outputs.Count);
        foreach (var port in outputs) {
            var channel = node.Mappings[port];
            if (!context.HasAny(channel)) {
                parts.Add($"[grey]{Markup.Escape(port.Name)}:[/] [dim]—[/]");
                continue;
            }
            var value = context.ReadAny(channel);
            parts.Add($"[silver]{Markup.Escape(port.Name)}:[/] {FormatValue(value)}");
        }
        return string.Join("  ", parts);
    }

    private static string FormatValue(object? value) {
        var (text, color) = value switch {
            null => ("null", "dim"),
            IStreamData or IBlob or IBufferData => (value.GetType().Name, "purple"),
            byte[] b => ($"bytes[{b.Length}]", "teal"),
            Array a => (FormatArray(a), "teal"),
            string s => (s.Length > 60 ? s[..60] + "…" : s, "yellow"),
            _ => (value.ToString() ?? "null", "white")
        };
        return $"[{color}]{Markup.Escape(text)}[/]";
    }

    private static string FormatArray(Array array) {
        if (array.Length == 0) return "[]";
        var sb = new StringBuilder("[");
        var shown = Math.Min(array.Length, 5);
        for (var i = 0; i < shown; i++) {
            if (i > 0) sb.Append(", ");
            sb.Append(array.GetValue(i));
        }
        if (array.Length > shown) sb.Append(", …");
        sb.Append($"] x{array.Length}");
        return sb.ToString();
    }

    private static string FormatDurationColumn(NodeResult result) {
        if (result.State == NodeState.Skipped) return "[dim]—[/]";
        return result.FromCache ? "[cyan]cache[/]" : FormatDuration(result.Elapsed);
    }

    private static string FormatDuration(TimeSpan t) {
        if (t.TotalMilliseconds < 1) return "<1ms";
        return t.TotalSeconds < 1 ? $"{t.TotalMilliseconds:F1}ms" : $"{t.TotalSeconds:F2}s";
    }

    private static (string Icon, string Color) StatusDisplay(NodeResult result) {
        return result.State switch {
            NodeState.Done when result.FromCache => ("♻", "cyan"),
            NodeState.Done => ("✔", "green"),
            NodeState.Skipped => ("»", "yellow"),
            NodeState.Failed => ("✘", "red"),
            _ => ("?", "grey")
        };
    }

    private static string Simplify(string typeName) {
        var i = typeName.LastIndexOf('`');
        if (i < 0) return typeName;
        return typeName[..i];
    }

    private readonly record struct NodeResult(NodeState State, TimeSpan Elapsed, bool FromCache, Exception? Error);
}
