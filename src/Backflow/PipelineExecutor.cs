using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Shiron.Backflow.BlobStorage;
using Shiron.Backflow.Caching;
using Shiron.Backflow.Context;
using Shiron.Backflow.Events;
using Shiron.Backflow.Exceptions;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Types;

namespace Shiron.Backflow;

/// <summary>
/// Executes a <see cref="Pipeline"/> topology layer-by-layer (topological order).
/// Supports optional per-node caching via <see cref="ICache"/> and blob storage via <see cref="IBlobStorageResolver"/>,
/// and feeds execution lifecycle events to an optional <see cref="IPipelineEventListener"/>.
/// </summary>
public class PipelineExecutor(
    Pipeline pipeline,
    ICache? cache = null,
    ICacheKeyFactory? keyFactory = null,
    CacheTypeAdapterRegistry? typeAdapters = null,
    IBlobStorageResolver? blobResolver = null,
    IPipelineEventListener? listener = null
) {
    /// <summary>The execution layers, each layer contains nodes that can run in parallel.</summary>
    public PipelineBuilder.NodeInstance[][] Layers => _layers.Select(layer => layer.ToArray()).ToArray();

    private readonly PipelineBuilder.NodeInstance[][] _layers = SnapshotLayers(pipeline);
    private readonly Dictionary<string, int> _indices = pipeline.Topology.ToLayers().SelectMany(layer => layer)
        .Select((node, index) => (node.ID, index)).ToDictionary(pair => pair.ID, pair => pair.index);
    private readonly Dictionary<string, IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>> _indexedInputs =
        BuildAllIndexedInputs(pipeline.Edges);

    private static PipelineBuilder.NodeInstance[][] SnapshotLayers(Pipeline pipeline) => pipeline.Topology.ToLayers()
        .Select(layer => layer.Select(node => node with {
            Mappings = node.Mappings.ToFrozenDictionary(),
            ArrayCounts = node.ArrayCounts?.ToFrozenDictionary()
        }).ToArray()).ToArray();

    private static Dictionary<string, IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>> BuildAllIndexedInputs(PipelineBuilder.EdgeInstance[] edges) =>
        edges.Where(edge => edge.DestIndex.HasValue).GroupBy(edge => edge.DestinationNode.ID)
            .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>)
                group.GroupBy(edge => edge.DestinationPort).ToDictionary(ports => ports.Key,
                    ports => (IReadOnlyList<(int Index, int SourceChannel)>) ports.Select(edge =>
                        (edge.DestIndex!.Value, edge.SourceNode.Mappings[edge.SourcePort])).ToArray()));

    private static readonly IReadOnlyDictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>> EmptyIndexedInputs =
        new Dictionary<IPort, IReadOnlyList<(int Index, int SourceChannel)>>();

    private readonly ICacheKeyFactory _keyFactory = keyFactory ?? new CacheKeyFactory(typeAdapters);
    private readonly IPipelineEventListener? _listener = listener;
    private readonly Dictionary<string, List<PipelineBuilder.EdgeInstance>> _incomingEdges = BuildIncomingEdges(pipeline.Edges);

    private static Dictionary<string, List<PipelineBuilder.EdgeInstance>> BuildIncomingEdges(PipelineBuilder.EdgeInstance[] edges) {
        var result = new Dictionary<string, List<PipelineBuilder.EdgeInstance>>();
        foreach (var edge in edges) {
            if (!result.TryGetValue(edge.DestinationNode.ID, out var list)) {
                list = [];
                result[edge.DestinationNode.ID] = list;
            }
            list.Add(edge);
        }
        return result;
    }

    private int TotalNodeCount => _indices.Count;

    /// <summary>
    /// Execute the pipeline synchronously, layer by layer. Nodes within a layer run sequentially.
    /// </summary>
    /// <param name="global">The shared context for reading/writing port values.</param>
    /// <param name="ct">Cancellation token observed between layers and after each node (cooperative).</param>
    /// <returns>Execution statistics including cache hit/miss counts and timing.</returns>
    public ExecutionStats Execute(IPipelineContext global, CancellationToken ct = default) =>
        ExecuteCoreAsync(global, false, ct).GetAwaiter().GetResult();

    /// <summary>
    /// Executes asynchronously. Independent asynchronous nodes overlap without dispatching synchronous nodes to the thread pool.
    /// </summary>
    /// <param name="global">The shared context for reading/writing port values.</param>
    /// <param name="ct">Cancellation token observed between layers and before each node (cooperative).</param>
    /// <returns>Execution statistics including cache hit/miss counts and timing.</returns>
    public Task<ExecutionStats> ExecuteAsync(IPipelineContext global, CancellationToken ct = default) =>
        ExecuteValueAsync(global, ct).AsTask();

    /// <summary>Executes with a synchronous-completion fast path that avoids a result task allocation.</summary>
    public ValueTask<ExecutionStats> ExecuteValueAsync(IPipelineContext global, CancellationToken ct = default) =>
        ExecuteCoreAsync(global, true, ct);

    private readonly PipelineBuilder.NodeInstance[] _nodes = SnapshotLayers(pipeline).SelectMany(layer => layer).ToArray();
    private readonly int[] _outputChannels = pipeline.Topology.Nodes.SelectMany(node =>
        node.Node.Outputs.Select(port => node.Mappings[port])).Distinct().ToArray();

    private async ValueTask<ExecutionStats> ExecuteCoreAsync(IPipelineContext global, bool parallel, CancellationToken ct) {
        global.Execution.Begin(this, _nodes);
        try {
            ct.ThrowIfCancellationRequested();
            if (global.Execution.HasRun) {
                for (var channelIndex = 0; channelIndex < _outputChannels.Length; channelIndex++)
                    global.Clear(_outputChannels[channelIndex]);
            }
            var started = Stopwatch.GetTimestamp();
            int executed = 0, skipped = 0, cacheHits = 0, cacheMisses = 0;
            _listener?.OnPipelineStart(new PipelineStartEvent(_layers.Length, TotalNodeCount));
            for (var li = 0; li < _layers.Length; li++) {
                ct.ThrowIfCancellationRequested();
                var layer = _layers[li];
                var layerStarted = Stopwatch.GetTimestamp();
                _listener?.OnLayerStart(new PipelineLayerStartEvent(li, layer.Length, layer.ToArray()));
                List<Task<NodeResult>>? pending = null;
                for (var ni = 0; ni < layer.Length; ni++) {
                    var operation = ExecuteOneAsync(layer[ni], li, global, ct);
                    if (parallel && !operation.IsCompletedSuccessfully) {
                        (pending ??= []).Add(operation.AsTask());
                        continue;
                    }
                    var result = await operation.ConfigureAwait(false);
                    Accumulate(result, ref executed, ref skipped, ref cacheHits, ref cacheMisses);
                }
                if (pending is not null) {
                    // Join every started operation before releasing the context, including on failure.
                    var results = await Task.WhenAll(pending).ConfigureAwait(false);
                    foreach (var result in results) Accumulate(result, ref executed, ref skipped, ref cacheHits, ref cacheMisses);
                }
                ct.ThrowIfCancellationRequested();
                _listener?.OnLayerComplete(new PipelineLayerCompleteEvent(li, layer.Length, Stopwatch.GetElapsedTime(layerStarted)));
            }
            var stats = new ExecutionStats(TotalNodeCount, executed, skipped, cacheHits, cacheMisses, Stopwatch.GetElapsedTime(started));
            _listener?.OnPipelineComplete(new PipelineCompleteEvent(_layers.Length, TotalNodeCount, stats));
            return stats;
        } finally {
            global.Execution.End();
        }
    }

    private readonly record struct NodeResult(bool Skipped, bool CacheHit, bool CacheMiss);

    private static void Accumulate(NodeResult result, ref int executed, ref int skipped, ref int hits, ref int misses) {
        if (result.Skipped) skipped++; else executed++;
        if (result.CacheHit) hits++;
        if (result.CacheMiss) misses++;
    }

    private async ValueTask<NodeResult> ExecuteOneAsync(PipelineBuilder.NodeInstance node, int layer, IPipelineContext global, CancellationToken ct) {
        ct.ThrowIfCancellationRequested();
        var index = _indices[node.ID];
        if (ShouldSkipDueToPropagation(node, global)) {
            global.Execution.Set(index, NodeState.Skipped);
            _listener?.OnNodeSkip(new PipelineNodeSkipEvent(node, layer));
            return new NodeResult(true, false, false);
        }
        var masks = AssembleFrozenArrayInputs(node, global);
        if (await TryCacheHitAsync(node, global).ConfigureAwait(false)) {
            global.Execution.Set(index, NodeState.Done);
            _listener?.OnNodeSuccess(new PipelineNodeSuccessEvent(node, layer, TimeSpan.Zero, FromCache: true));
            return new NodeResult(false, true, false);
        }
        var cacheable = IsNodeCacheable(node);
        global.Execution.Set(index, NodeState.Executing);
        _listener?.OnNodeStart(new PipelineNodeStartEvent(node, layer));
        var started = Stopwatch.GetTimestamp();
        try {
            await ExecuteNodeAsync(node, global, masks, ct).ConfigureAwait(false);
            if (global.Execution.Get(index) == NodeState.Done)
                await CacheOutputsAsync(node, global, ct).ConfigureAwait(false);
        } catch (Exception ex) {
            _listener?.OnNodeFailure(new PipelineNodeFailureEvent(node, layer, ExtractInner(ex), Stopwatch.GetElapsedTime(started)));
            throw;
        }
        ct.ThrowIfCancellationRequested();
        _listener?.OnNodeSuccess(new PipelineNodeSuccessEvent(node, layer, Stopwatch.GetElapsedTime(started), FromCache: false));
        return new NodeResult(global.Execution.Get(index) == NodeState.Skipped, false, cacheable);
    }

    private Dictionary<IPort, BitArray>? AssembleFrozenArrayInputs(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (node.ArrayCounts is null) return null;
        var masks = new Dictionary<IPort, BitArray>();
        var indexedByPort = _indexedInputs.GetValueOrDefault(node.ID, EmptyIndexedInputs);

        foreach (var (port, count) in node.ArrayCounts) {
            var targetChannel = node.Mappings[port];
            var sources = indexedByPort.TryGetValue(port, out var s)
                ? s
                : [];
            var writeAtMask = global.GetSuppliedMask(targetChannel);

            if (sources.Count == 0 && writeAtMask is null) continue;

            var mask = ((IArrayPortAssembly) port).Assemble(global, targetChannel, sources, count);

            if (writeAtMask is not null) {
                for (var i = 0; i < mask.Length; i++) {
                    var word = i >> 5;
                    if (word >= writeAtMask.Length) break;
                    if ((writeAtMask[word] & (1 << (i & 31))) != 0) mask[i] = true;
                }
            }

            masks[port] = mask;
        }

        return masks;
    }

    private bool ShouldSkipDueToPropagation(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return false;

        foreach (var edge in edges) {
            if (edge.DestIndex.HasValue) continue;

            if (global.Execution.Get(_indices[edge.SourceNode.ID]) != NodeState.Skipped) continue;
            if (edge.DestinationPort is Port.Port { IsRequired: true }) return true;
        }

        return false;
    }

    private async ValueTask ExecuteNodeAsync(PipelineBuilder.NodeInstance node, IPipelineContext global, Dictionary<IPort, BitArray>? suppliedMasks, CancellationToken ct) {
        var index = _indices[node.ID];
        var context = global.Execution.GetContext(index);
        if (context is null) {
            context = new NodeContext(global, node.Mappings, _indexedInputs.GetValueOrDefault(node.ID, EmptyIndexedInputs));
            global.Execution.SetContext(index, context);
        }
        context.Prepare(ct, suppliedMasks);

        NodeState state;
        try {
            state = await node.Node.ExecuteAsync(context).ConfigureAwait(false);
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            global.Execution.Set(index, NodeState.Canceled);
            throw;
        } catch (Exception ex) {
            global.Execution.Set(index, NodeState.Failed);
            throw new NodeExecutionException(node, ex);
        }

        global.Execution.Set(index, state);
        if (state == NodeState.Failed) throw new NodeExecutionException(node);
    }

    /// <summary>Unwraps <see cref="NodeExecutionException"/> so listeners see the original cause.</summary>
    private static Exception ExtractInner(Exception ex) =>
        ex is NodeExecutionException nee && nee.InnerException is not null ? nee.InnerException : ex;


    private bool IsNodeCacheable(PipelineBuilder.NodeInstance node) {
        if (cache is null || !node.Node.UseCache) return false;

        if (blobResolver is null) {
            foreach (var port in node.Node.Ports) {
                var portType = port.PortType;
                if (portType is null) continue;

                if (typeof(IStreamData).IsAssignableFrom(portType) ||
                    typeof(IBlob).IsAssignableFrom(portType) ||
                    typeof(IBufferData).IsAssignableFrom(portType)) {
                    return false;
                }
            }
        }

        return true;
    }

    private async ValueTask<bool> TryCacheHitAsync(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!IsNodeCacheable(node)) return false;

        var key = _keyFactory.CreateKey(node, global);
        var (found, entry) = await cache!.TryGetAsync(key).ConfigureAwait(false);
        if (!found || entry is null) return false;

        try {
            RestoreOutputs(node, global, entry);
            return true;
        } catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException) {
            foreach (var port in node.Node.Outputs) global.Clear(node.Mappings[port]);
            await cache.RemoveAsync(key).ConfigureAwait(false);
            return false;
        }
    }

    private async ValueTask CacheOutputsAsync(PipelineBuilder.NodeInstance node, IPipelineContext global, CancellationToken ct) {
        if (!IsNodeCacheable(node)) return;

        var key = _keyFactory.CreateKey(node, global);
        var entry = CaptureOutputs(node, global, ct);
        await cache!.SetAsync(key, entry);
    }

    private ICacheEntry CaptureOutputs(PipelineBuilder.NodeInstance node, IPipelineContext global, CancellationToken ct) {
        var inputs = new Dictionary<string, CachePortValue>();
        var outputs = new Dictionary<string, CachePortValue>();

        foreach (var port in node.Node.Inputs) {
            var channel = node.Mappings[port];
            if (!global.HasAny(channel)) continue;

            var value = global.ReadAny(channel);
            value = TryStoreBlob(value, ct);
            var typeName = value?.GetType().AssemblyQualifiedName ?? "null";
            inputs[port.Name] = new CachePortValue(value, typeName);
        }

        foreach (var port in node.Node.Outputs) {
            var channel = node.Mappings[port];
            if (!global.HasAny(channel)) continue;

            var value = global.ReadAny(channel);
            value = TryStoreBlob(value, ct);
            var typeName = value?.GetType().AssemblyQualifiedName ?? "null";
            outputs[port.Name] = new CachePortValue(value, typeName);
        }

        return new CacheEntry {
            Inputs = inputs,
            Outputs = outputs,
            NodeTypeName = node.Node.GetType().FullName ?? node.Node.GetType().Name,
        };
    }

    private object? TryStoreBlob(object? value, CancellationToken ct) {
        if (blobResolver is null || value is null) return value;

        return value switch {
            IBufferData bufferData => StoreBlobFromBuffer(bufferData, ct),
            IStreamData streamData => StoreBlobFromStream(streamData, ct),
            IBlob blob => StoreBlobFromBlob(blob, ct),
            _ => value
        };
    }

    private BlobCacheEntry StoreBlobFromBuffer(IBufferData bufferData, CancellationToken ct) {
        var data = bufferData.Data;
        var metadata = new BlobMetadata { ContentLength = data.Length };
        var storage = blobResolver!.Resolve(metadata);
        var blobId = storage.StoreAsync(new MemoryStream(data.ToArray()), metadata, ct).GetAwaiter().GetResult();
        return new BlobCacheEntry { ReferenceUri = new BlobReference(storage.Name, blobId).Uri.ToString() };
    }

    private BlobCacheEntry StoreBlobFromStream(IStreamData streamData, CancellationToken ct) {
        var stream = streamData.OpenRead();
        var storage = blobResolver!.Resolve(null);
        var blobId = storage.StoreAsync(stream, null, ct).GetAwaiter().GetResult();
        stream.Dispose();
        return new BlobCacheEntry { ReferenceUri = new BlobReference(storage.Name, blobId).Uri.ToString() };
    }

    private BlobCacheEntry StoreBlobFromBlob(IBlob blob, CancellationToken ct) {
        var stream = blob.Storage.OpenRead();
        var storage = blobResolver!.Resolve(null);
        var blobId = storage.StoreAsync(stream, null, ct).GetAwaiter().GetResult();
        stream.Dispose();

        var reference = new BlobReference(storage.Name, blobId);

        string? metaJson = null;
        string? metaTypeName = null;

        var typedInterface = blob.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBlob<,>));

        if (typedInterface is not null) {
            var args = typedInterface.GetGenericArguments();
            var metaProp = typedInterface.GetProperty("Meta");
            if (metaProp?.GetValue(blob) is { } meta) {
                metaJson = JsonSerializer.Serialize(meta, args[0]);
                metaTypeName = args[0].AssemblyQualifiedName;
            }
        }

        return new BlobCacheEntry { ReferenceUri = reference.Uri.ToString(), MetaJson = metaJson, MetaTypeName = metaTypeName };
    }

    private void RestoreOutputs(PipelineBuilder.NodeInstance node, IPipelineContext global, ICacheEntry entry) {
        foreach (var port in node.Node.Outputs) {
            global.Clear(node.Mappings[port]);
            if (!entry.Outputs.TryGetValue(port.Name, out var cached)) continue;

            var channel = node.Mappings[port];
            var value = cached.Value;

            if (value is BlobCacheEntry blobEntry) {
                var restored = RestoreBlobEntry(blobEntry, port.PortType!);
                if (!port.PortType.IsInstanceOfType(restored))
                    throw new InvalidDataException("The restored blob does not match its declared output port contract.");
                var storageType = ResolveStorageType(restored, port.PortType!);
                global.Write(channel, restored, storageType);
                continue;
            }

            if (value is BlobReference blobRef) {
                var cachedStream = new CachedStreamData(blobRef, blobResolver!);
                using (var validationStream = cachedStream.OpenRead()) validationStream.CopyTo(Stream.Null);
                object restored;
                Type storageType;

                if (typeof(IBlob).IsAssignableFrom(port.PortType)) {
                    restored = new CachedBlob(cachedStream);
                    storageType = typeof(IBlob);
                } else {
                    restored = cachedStream;
                    storageType = typeof(IStreamData);
                }

                global.Write(channel, restored, storageType);
                continue;
            }

            if (value is not null && !port.PortType.IsInstanceOfType(value))
                throw new InvalidDataException($"Cached output '{port.Name}' does not match its declared port contract.");
            global.Write(channel, value, port.PortType);
        }
    }

    private object RestoreBlobEntry(BlobCacheEntry entry, Type portType) {
        if (blobResolver is null) throw new InvalidDataException("Cached blobs require a blob storage resolver.");
        var cachedStream = new CachedStreamData(entry.Reference, blobResolver!);
        using (var validationStream = cachedStream.OpenRead()) validationStream.CopyTo(Stream.Null);

        if (entry.HasMeta) {
            var blobContract = portType.IsGenericType && portType.GetGenericTypeDefinition() == typeof(IBlob<,>)
                ? portType
                : portType.GetInterfaces().FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IBlob<,>));
            var metaType = blobContract?.GetGenericArguments()[0];
            if (metaType is null || metaType.AssemblyQualifiedName != entry.MetaTypeName)
                throw new InvalidDataException("Cached blob metadata is not declared by the output port contract.");
            if (metaType is not null && JsonSerializer.Deserialize(entry.MetaJson!, metaType) is { } meta) {
                using var stream = cachedStream.OpenRead();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bufferData = new BufferData(ms.ToArray());

                var blobType = typeof(Blob<,>).MakeGenericType(metaType, typeof(BufferData));
                return Activator.CreateInstance(blobType, meta, bufferData)!;
            }
        }

        if (typeof(IBufferData).IsAssignableFrom(portType)) {
            using var stream = cachedStream.OpenRead();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return new BufferData(buffer.ToArray());
        }

        if (typeof(IBlob).IsAssignableFrom(portType)) {
            return new CachedBlob(cachedStream);
        }

        return cachedStream;
    }

    private static Type ResolveStorageType(object restored, Type portType) {
        if (portType.IsInstanceOfType(restored)) {
            return portType;
        }

        if (restored is CachedBlob) {
            return typeof(IBlob);
        }

        return typeof(IStreamData);
    }
}

internal interface IArrayPortAssembly {
    BitArray Assemble(IPipelineContext context, int targetChannel, IReadOnlyList<(int Index, int SourceChannel)> sources, int count);
}
