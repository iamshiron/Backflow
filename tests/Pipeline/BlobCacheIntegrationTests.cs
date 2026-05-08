using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Types;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class BlobCacheIntegrationTests : IAsyncLifetime {
    private string _tmpDir = null!;

    public Task InitializeAsync() {
        _tmpDir = Path.Combine(Path.GetTempPath(), $"blob-cache-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() {
        if (Directory.Exists(_tmpDir)) {
            Directory.Delete(_tmpDir, true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BlobNode_WithBlobStore_CachesAndRestores() {
        var cachePath = Path.Combine(_tmpDir, "cache.json");
        var blobDir = Path.Combine(_tmpDir, "blobs");

        var registry = new NodeRegistry();
        var producer = registry.Register<BlobProducerNode>();
        var consumer = registry.Register<BlobSizeNode>();

        var builder = new PipelineBuilder(registry);
        var producerInst = builder.AddNode(producer);
        var consumerInst = builder.AddNode(consumer);
        builder.AddConnection(producerInst, producer.Out, consumerInst, consumer.Input);

        var context = new PipelineContext();
        context.Write(producerInst, producer.InputValue, 42);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await using var blobStore = new FileSystemBlobStore(blobDir);
        await using var cache = new JsonFileNodeCache(cachePath, blobStore);

        var stats1 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(2, stats1.ExecutedNodes);
        Assert.Equal(0, stats1.CacheHits);

        await blobStore.FlushAsync();
        await cache.FlushAsync();

        Assert.True(context.Has<int>(consumerInst, consumer.OutputSize));
        Assert.Equal(42, context.Read<int>(consumerInst, consumer.OutputSize));

        var stats2 = await executor.ExecuteAsync(context, cache);
        Assert.True(stats2.CacheHits >= 1, $"Expected at least 1 cache hit, got {stats2.CacheHits}. Full stats: {stats2}");
        Assert.True(stats2.ExecutedNodes == 0, $"Expected 0 executions on second run, got {stats2.ExecutedNodes}. Full stats: {stats2}");
    }

    [Fact]
    public async Task BlobNode_WithoutBlobStore_AlwaysMisses() {
        var cachePath = Path.Combine(_tmpDir, "cache-no-blob.json");

        var registry = new NodeRegistry();
        var producer = registry.Register<BlobProducerNode>();
        var consumer = registry.Register<BlobSizeNode>();

        var builder = new PipelineBuilder(registry);
        var producerInst = builder.AddNode(producer);
        var consumerInst = builder.AddNode(consumer);
        builder.AddConnection(producerInst, producer.Out, consumerInst, consumer.Input);

        var context = new PipelineContext();
        context.Write(producerInst, producer.InputValue, 42);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await using var cache = new JsonFileNodeCache(cachePath);

        var stats1 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(2, stats1.ExecutedNodes);

        await cache.FlushAsync();

        var stats2 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(1, stats2.CacheHits);
        Assert.Equal(1, stats2.ExecutedNodes);
    }

    [Fact]
    public async Task PrimitiveNode_StillCaches_WhenNoBlobStore() {
        var cachePath = Path.Combine(_tmpDir, "cache-primitive.json");

        var registry = new NodeRegistry();
        var add = registry.Register<AddNode>();

        var builder = new PipelineBuilder(registry);
        var addInst = builder.AddNode(add);

        var context = new PipelineContext();
        context.Write(addInst, add.Number1, 10);
        context.Write(addInst, add.Number2, 20);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await using var cache = new JsonFileNodeCache(cachePath);

        var stats1 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(1, stats1.ExecutedNodes);
        Assert.Equal(30, context.Read<int>(addInst, add.Sum));

        await cache.FlushAsync();

        var stats2 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(1, stats2.CacheHits);
    }

    [Fact]
    public async Task MemoryBlobStore_WorksEndToEnd() {
        var cachePath = Path.Combine(_tmpDir, "cache-memory.json");

        var registry = new NodeRegistry();
        var producer = registry.Register<BlobProducerNode>();

        var builder = new PipelineBuilder(registry);
        var producerInst = builder.AddNode(producer);

        var context = new PipelineContext();
        context.Write(producerInst, producer.InputValue, 99);
        var pipeline = builder.Build();
        var executor = new PipelineExecutor(pipeline);

        await using var blobStore = new MemoryBlobStore();
        await using var cache = new JsonFileNodeCache(cachePath, blobStore);

        var stats1 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(1, stats1.ExecutedNodes);
        await cache.FlushAsync();

        var stats2 = await executor.ExecuteAsync(context, cache);
        Assert.Equal(1, stats2.CacheHits);
    }

    private class BlobProducerNode : AbstractNode {
        public IInputPort<int> InputValue { get; }
        public IOutputPort<MemoryBlob> Out { get; }

        public BlobProducerNode() {
            InputValue = Input(new NumericPortBuilder<int>(nameof(InputValue)).Input());
            Out = Output(new BlobPortBuilder<MemoryBlob>(nameof(Out)).Output());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var value = InputValue.Read(context);
            var data = new byte[value];
            for (var i = 0; i < value; i++) data[i] = (byte) (i % 256);
            Out.Write(context, new MemoryBlob { Data = data });
            return ValueTask.FromResult(true);
        }
    }

    private class BlobSizeNode : AbstractNode {
        public IInputPort<MemoryBlob> Input { get; }
        public IOutputPort<int> OutputSize { get; }

        public BlobSizeNode() {
            Input = Input(new BlobPortBuilder<MemoryBlob>(nameof(Input)).Input());
            OutputSize = Output(new NumericPortBuilder<int>(nameof(OutputSize)).Output());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            var blob = Input.Read(context)!;
            OutputSize.Write(context, blob.Data.Length);
            return ValueTask.FromResult(true);
        }
    }

    private class AddNode : AbstractNode {
        public IInputPort<int> Number1 { get; }
        public IInputPort<int> Number2 { get; }
        public IOutputPort<int> Sum { get; }

        public AddNode() {
            Number1 = Input(new NumericPortBuilder<int>(nameof(Number1)).Input());
            Number2 = Input(new NumericPortBuilder<int>(nameof(Number2)).Input());
            Sum = Output(new NumericPortBuilder<int>(nameof(Sum)).Output());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Sum.Write(context, Number1.Read(context) + Number2.Read(context));
            return ValueTask.FromResult(true);
        }
    }
}
