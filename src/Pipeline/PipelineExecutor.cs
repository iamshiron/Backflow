using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Shiron.Lib.Pipeline.BlobStorage;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Exceptions;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Types;

namespace Shiron.Lib.Pipeline;

/// <summary>
/// Executes a <see cref="Pipeline"/> topology layer-by-layer (topological order).
/// Supports optional per-node caching via <see cref="ICache"/> and blob storage via <see cref="IBlobStorageResolver"/>.
/// </summary>
public class PipelineExecutor(Pipeline pipeline, ICache? cache = null, ICacheKeyFactory? keyFactory = null, CacheTypeAdapterRegistry? typeAdapters = null, IBlobStorageResolver? blobResolver = null) {
    /// <summary>The execution layers, each layer contains nodes that can run in parallel.</summary>
    public PipelineBuilder.NodeInstance[][] Layers { get; } = pipeline.Topology.ToLayers();

    private readonly ICacheKeyFactory _keyFactory = keyFactory ?? new CacheKeyFactory(typeAdapters);
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

    private int TotalNodeCount => Layers.Sum(l => l.Length);

    /// <summary>
    /// Execute the pipeline synchronously, layer by layer. Nodes within a layer run sequentially.
    /// </summary>
    /// <param name="global">The shared context for reading/writing port values.</param>
    /// <returns>Execution statistics including cache hit/miss counts and timing.</returns>
    public ExecutionStats Execute(IPipelineContext global) {
        var sw = Stopwatch.StartNew();
        int executed = 0, skipped = 0, cacheHits = 0, cacheMisses = 0;

        foreach (var layer in Layers) {
            foreach (var node in layer) {
                if (ShouldSkipDueToPropagation(node)) {
                    node.State = NodeState.Skipped;
                    skipped++;
                    continue;
                }

                AssembleFrozenArrayInputs(node, global);

                if (TryCacheHit(node, global)) {
                    node.State = NodeState.Done;
                    cacheHits++;
                    executed++;
                    continue;
                }

                if (IsNodeCacheable(node)) cacheMisses++;

                ExecuteNodeAsync(node, global).GetAwaiter().GetResult();
                CacheOutputs(node, global);
                executed++;
            }
        }

        sw.Stop();
        return new ExecutionStats(TotalNodeCount, executed, skipped, cacheHits, cacheMisses, sw.Elapsed);
    }

    /// <summary>
    /// Execute the pipeline asynchronously, with nodes within each layer running in parallel via <see cref="Task.Run"/>.
    /// </summary>
    /// <param name="global">The shared context for reading/writing port values.</param>
    /// <returns>Execution statistics including cache hit/miss counts and timing.</returns>
    public async Task<ExecutionStats> ExecuteAsync(IPipelineContext global) {
        var sw = Stopwatch.StartNew();
        int executed = 0, skipped = 0, cacheHits = 0, cacheMisses = 0;

        foreach (var layer in Layers) {
            List<Task> tasks = [];
            foreach (var node in layer) {
                tasks.Add(Task.Run(async () => {
                    if (ShouldSkipDueToPropagation(node)) {
                        node.State = NodeState.Skipped;
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    AssembleFrozenArrayInputs(node, global);

                    if (TryCacheHit(node, global)) {
                        node.State = NodeState.Done;
                        Interlocked.Increment(ref cacheHits);
                        Interlocked.Increment(ref executed);
                        return;
                    }

                    if (IsNodeCacheable(node)) Interlocked.Increment(ref cacheMisses);

                    await ExecuteNodeAsync(node, global);
                    await CacheOutputsAsync(node, global);
                    Interlocked.Increment(ref executed);
                }));
            }
            await Task.WhenAll(tasks);
        }

        sw.Stop();
        return new ExecutionStats(TotalNodeCount, executed, skipped, cacheHits, cacheMisses, sw.Elapsed);
    }

    private void AssembleFrozenArrayInputs(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return;

        var indexedByPort = new Dictionary<IPort, List<(int Index, Guid SourceGuid)>>();

        foreach (var edge in edges) {
            if (!edge.DestIndex.HasValue) continue;

            if (!indexedByPort.TryGetValue(edge.DestinationPort, out var list)) {
                list = [];
                indexedByPort[edge.DestinationPort] = list;
            }
            list.Add((edge.DestIndex.Value, edge.SourceNode.Mappings[edge.SourcePort]));
        }

        foreach (var (port, sources) in indexedByPort) {
            if (port is not IArrayInputPortMarker { IsFrozen: true }) continue;

            var targetGuid = node.Mappings[port];
            ((IArrayPortAssembly) port).Assemble(global, targetGuid, sources);
        }
    }

    private Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>> BuildIndexedInputs(PipelineBuilder.NodeInstance node) {
        var result = new Dictionary<IPort, IReadOnlyList<(int Index, Guid SourceGuid)>>();

        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return result;

        foreach (var edge in edges) {
            if (!edge.DestIndex.HasValue) continue;

            if (!result.TryGetValue(edge.DestinationPort, out var list)) {
                list = new List<(int Index, Guid SourceGuid)>();
                result[edge.DestinationPort] = list;
            }

            ((List<(int Index, Guid SourceGuid)>) list).Add((edge.DestIndex.Value, edge.SourceNode.Mappings[edge.SourcePort]));
        }

        return result;
    }

    private bool ShouldSkipDueToPropagation(PipelineBuilder.NodeInstance node) {
        if (!_incomingEdges.TryGetValue(node.ID, out var edges)) return false;

        foreach (var edge in edges) {
            if (edge.DestIndex.HasValue) continue;

            if (edge.SourceNode.State != NodeState.Skipped) continue;
            if (edge.DestinationPort is Port.Port { IsRequired: true }) return true;
        }

        return false;
    }

    private async Task ExecuteNodeAsync(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        var indexedInputs = BuildIndexedInputs(node);
        var context = new NodeContext(global, node.Mappings, indexedInputs);

        NodeState state;
        try {
            state = await node.Node.ExecuteAsync(context);
        } catch (Exception ex) {
            Console.WriteLine($"Error executing node {node.ID}: {ex.Message}");
            node.State = NodeState.Failed;
            throw new NodeExecutionException(node, ex);
        }

        node.State = state;
        if (state == NodeState.Failed) throw new NodeExecutionException(node);
    }

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

    private bool TryCacheHit(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!IsNodeCacheable(node)) return false;

        var key = _keyFactory.CreateKey(node, global);
        var (found, entry) = cache!.TryGetAsync(key).GetAwaiter().GetResult();
        if (!found || entry is null) return false;

        RestoreOutputs(node, global, entry);
        return true;
    }

    private void CacheOutputs(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!IsNodeCacheable(node)) return;

        var key = _keyFactory.CreateKey(node, global);
        var entry = CaptureOutputs(node, global);
        cache!.SetAsync(key, entry).GetAwaiter().GetResult();
    }

    private async Task CacheOutputsAsync(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        if (!IsNodeCacheable(node)) return;

        var key = _keyFactory.CreateKey(node, global);
        var entry = CaptureOutputs(node, global);
        await cache!.SetAsync(key, entry);
    }

    private ICacheEntry CaptureOutputs(PipelineBuilder.NodeInstance node, IPipelineContext global) {
        var inputs = new Dictionary<string, CachePortValue>();
        var outputs = new Dictionary<string, CachePortValue>();

        foreach (var port in node.Node.Inputs) {
            var guid = node.Mappings[port];
            if (!global.HasAny(guid)) continue;

            var value = global.ReadAny(guid);
            value = TryStoreBlob(value);
            var typeName = value?.GetType().AssemblyQualifiedName ?? "null";
            inputs[port.Name] = new CachePortValue(value, typeName);
        }

        foreach (var port in node.Node.Outputs) {
            var guid = node.Mappings[port];
            if (!global.HasAny(guid)) continue;

            var value = global.ReadAny(guid);
            value = TryStoreBlob(value);
            var typeName = value?.GetType().AssemblyQualifiedName ?? "null";
            outputs[port.Name] = new CachePortValue(value, typeName);
        }

        return new CacheEntry {
            Inputs = inputs,
            Outputs = outputs,
            NodeTypeName = node.Node.GetType().FullName ?? node.Node.GetType().Name,
        };
    }

    private object? TryStoreBlob(object? value) {
        if (blobResolver is null || value is null) return value;

        return value switch {
            IBufferData bufferData => StoreBlobFromBuffer(bufferData),
            IStreamData streamData => StoreBlobFromStream(streamData),
            IBlob blob => StoreBlobFromBlob(blob),
            _ => value
        };
    }

    private BlobCacheEntry StoreBlobFromBuffer(IBufferData bufferData) {
        var data = bufferData.Data;
        var metadata = new BlobMetadata { ContentLength = data.Length };
        var storage = blobResolver!.Resolve(metadata);
        var blobId = storage.StoreAsync(new MemoryStream(data.ToArray()), metadata).GetAwaiter().GetResult();
        return new BlobCacheEntry { ReferenceUri = new BlobReference(storage.Name, blobId).Uri.ToString() };
    }

    private BlobCacheEntry StoreBlobFromStream(IStreamData streamData) {
        var stream = streamData.OpenRead();
        var storage = blobResolver!.Resolve(null);
        var blobId = storage.StoreAsync(stream).GetAwaiter().GetResult();
        stream.Dispose();
        return new BlobCacheEntry { ReferenceUri = new BlobReference(storage.Name, blobId).Uri.ToString() };
    }

    private BlobCacheEntry StoreBlobFromBlob(IBlob blob) {
        var stream = blob.Storage.OpenRead();
        var storage = blobResolver!.Resolve(null);
        var blobId = storage.StoreAsync(stream).GetAwaiter().GetResult();
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
        var typedStore = global as PipelineContext;

        foreach (var port in node.Node.Outputs) {
            if (!entry.Outputs.TryGetValue(port.Name, out var cached)) continue;

            var guid = node.Mappings[port];
            var type = Type.GetType(cached.TypeName);
            var value = cached.Value;

            if (value is BlobCacheEntry blobEntry) {
                var restored = RestoreBlobEntry(blobEntry, port.PortType!);
                var storageType = ResolveStorageType(restored, port.PortType!);
                if (typedStore is not null) {
                    typedStore.Store.Set(guid, restored, storageType);
                } else {
                    global.Write(guid, restored);
                }
                continue;
            }

            if (value is BlobReference blobRef) {
                var cachedStream = new CachedStreamData(blobRef, blobResolver!);
                object restored;
                Type storageType;

                if (typeof(IBlob).IsAssignableFrom(port.PortType)) {
                    restored = new CachedBlob(cachedStream);
                    storageType = typeof(IBlob);
                } else {
                    restored = cachedStream;
                    storageType = typeof(IStreamData);
                }

                if (typedStore is not null) {
                    typedStore.Store.Set(guid, restored, storageType);
                } else {
                    global.Write(guid, restored);
                }
                continue;
            }

            if (type is not null && typedStore is not null) {
                typedStore.Store.Set(guid, value, type);
            } else {
                global.Write(guid, value);
            }
        }
    }

    private object RestoreBlobEntry(BlobCacheEntry entry, Type portType) {
        var cachedStream = new CachedStreamData(entry.Reference, blobResolver!);

        if (entry.HasMeta) {
            var metaType = Type.GetType(entry.MetaTypeName!);
            if (metaType is not null && JsonSerializer.Deserialize(entry.MetaJson!, metaType) is { } meta) {
                using var stream = cachedStream.OpenRead();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bufferData = new BufferData(ms.ToArray());

                var blobType = typeof(Blob<,>).MakeGenericType(metaType, typeof(BufferData));
                return Activator.CreateInstance(blobType, meta, bufferData)!;
            }
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
    void Assemble(IPipelineContext context, Guid targetGuid, IReadOnlyList<(int Index, Guid SourceGuid)> sources);
    void AssembleWithCount(IPipelineContext context, Guid targetGuid, IReadOnlyList<(int Index, Guid SourceGuid)> sources, int count);
}
