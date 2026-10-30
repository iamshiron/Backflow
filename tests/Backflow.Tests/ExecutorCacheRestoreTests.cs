using Shiron.Backflow.BlobStorage;
using Shiron.Backflow.Caching;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Node.Behvaior;
using Shiron.Backflow.Port;
using Shiron.Backflow.Registry;
using Shiron.Backflow.Types;
using Xunit;

namespace Shiron.Backflow.Tests;

public sealed class ExecutorCacheRestoreTests {
    private sealed class CacheNode : AbstractNode {
        public IOutputPort<int> Out { get; }
        public ChipEnableBehavior Enable { get; } = new();
        public CacheNode() {
            UseCache = true;
            Out = Output(new OutputPort<int>("out"));
            AddBehavior(Enable);
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write(Out, 42);
            return new ValueTask<bool>(true);
        }
    }

    private sealed class StreamNode : AbstractNode {
        public IOutputPort<IStreamData> Out { get; }
        public StreamNode() {
            UseCache = true;
            Out = Output(new OutputPort<IStreamData>("out"));
        }
        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            context.Write<IStreamData>(Out, new BufferData([1, 2, 3]));
            return new ValueTask<bool>(true);
        }
    }

    private sealed class MemoryCache(ICacheEntry? initial) : ICache {
        private ICacheEntry? _entry = initial;
        public int Removed { get; private set; }
        public int Stored { get; private set; }
        public ValueTask<(bool Found, ICacheEntry? Entry)> TryGetAsync(ICacheKey key) => new((_entry is not null, _entry));
        public ValueTask SetAsync(ICacheKey key, ICacheEntry entry) { _entry = entry; Stored++; return ValueTask.CompletedTask; }
        public ValueTask<bool> RemoveAsync(ICacheKey key) { _entry = null; Removed++; return new(true); }
        public ValueTask ClearAsync() { _entry = null; return ValueTask.CompletedTask; }
        public void Dispose() { }
    }

    private sealed class BrokenStorage(bool missing) : IBlobStorage, IBlobStorageResolver {
        public string Name => "test";
        public IBlobStorage Resolve(BlobMetadata? metadata) => this;
        public IBlobStorage ResolveByName(string storageName) => this;
        public ValueTask<string> StoreAsync(Stream data, BlobMetadata? metadata = null, CancellationToken ct = default) => new("recomputed");
        public ValueTask<Stream> OpenReadAsync(string blobId, CancellationToken ct = default) =>
            throw (missing ? new FileNotFoundException("missing") : new InvalidDataException("corrupt"));
        public ValueTask<BlobMetadata?> GetMetadataAsync(string blobId, CancellationToken ct = default) => new((BlobMetadata?) null);
        public ValueTask<bool> ExistsAsync(string blobId, CancellationToken ct = default) => new(false);
        public ValueTask<bool> RemoveAsync(string blobId, CancellationToken ct = default) => new(false);
        public ValueTask ClearAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    private static CacheEntry Entry(object value) => new() {
        Inputs = new Dictionary<string, CachePortValue>(),
        Outputs = new Dictionary<string, CachePortValue> { ["out"] = new(value, "Untrusted.Type, Untrusted.Assembly") },
        NodeTypeName = "untrusted"
    };

    [Fact]
    public async Task ExecuteAsync_IncompatibleCachedValue_EvictsAndRecomputes() {
        var node = new CacheNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var instance = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        using var cache = new MemoryCache(Entry("not an integer"));
        var stats = await new PipelineExecutor(pipeline, cache).ExecuteAsync(context);
        Assert.Equal(1, cache.Removed);
        Assert.Equal(1, cache.Stored);
        Assert.Equal(0, stats.CacheHits);
        Assert.Equal(42, context.Read<int>(instance, node.Out));
        Assert.Equal(NodeState.Done, context.Execution.GetNodeState(instance));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_MissingOrCorruptCachedBlob_EvictsAndRecomputes(bool missing) {
        var node = new StreamNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var instance = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        using var cache = new MemoryCache(Entry(new BlobCacheEntry { ReferenceUri = new BlobReference("test", "broken").Uri.ToString() }));
        using var storage = new BrokenStorage(missing);
        var stats = await new PipelineExecutor(pipeline, cache, blobResolver: storage).ExecuteAsync(context);
        Assert.Equal(1, cache.Removed);
        Assert.Equal(1, cache.Stored);
        Assert.Equal(0, stats.CacheHits);
        using var result = context.Read<IStreamData>(instance, node.Out)!.OpenRead();
        using var buffer = new MemoryStream();
        await result.CopyToAsync(buffer);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ToArray());
    }

    [Fact]
    public void Execute_BehaviorSkippedNode_DoesNotCacheAbsentOutputsAsSuccess() {
        var node = new CacheNode();
        var builder = new PipelineBuilder(new NodeRegistry());
        var instance = builder.AddNode(node);
        var pipeline = builder.Build();
        var context = ArrayPipelineContext.ForPipeline(pipeline);
        context.Write(instance, node.Enable.ChipEnable, false);
        using var cache = new MemoryCache(null);
        var stats = new PipelineExecutor(pipeline, cache).Execute(context);
        Assert.Equal(0, cache.Stored);
        Assert.Equal(1, stats.SkippedNodes);
        Assert.Equal(NodeState.Skipped, context.Execution.GetNodeState(instance));
    }
}
