using Shiron.Backflow.Caching;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port;
using Shiron.Backflow.Port.Builder;
using Shiron.Backflow.Registry;
using Xunit;

namespace Shiron.Backflow.Tests;

public sealed class ContextCacheIsolationTests {
    [Fact]
    public async Task ExecuteValueAsync_DifferentContextOptions_IsolatesPersistedCacheAcrossRuns() {
        var directory = Path.Combine(Path.GetTempPath(), $"backflow-context-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try {
            var node = new OffsetNode();
            var builder = new PipelineBuilder(new NodeRegistry());
            var instance = builder.AddNode(node);
            var pipeline = builder.Build();
            var first = ArrayPipelineContext.ForPipeline(pipeline, new Options(3));
            var second = ArrayPipelineContext.ForPipeline(pipeline, new Options(8));
            first.Write(instance, node.InputValue, 10);
            second.Write(instance, node.InputValue, 10);
            var path = Path.Combine(directory, "entries.json");
            using (var cache = new JsonFileCache(path)) {
                var executor = new PipelineExecutor(pipeline, cache);
                Assert.Equal(0, (await executor.ExecuteValueAsync(first)).CacheHits);
                Assert.Equal(13, first.Read<int>(instance, node.Result));
                Assert.Equal(0, (await executor.ExecuteValueAsync(second)).CacheHits);
                Assert.Equal(18, second.Read<int>(instance, node.Result));
                Assert.Equal(1, (await executor.ExecuteValueAsync(first)).CacheHits);
                Assert.Equal(13, first.Read<int>(instance, node.Result));
                Assert.Equal(2, node.Executions);
                await cache.FlushAsync();
            }
            using (var reopened = new JsonFileCache(path)) {
                var executor = new PipelineExecutor(pipeline, reopened);
                Assert.Equal(1, (await executor.ExecuteValueAsync(second)).CacheHits);
                Assert.Equal(18, second.Read<int>(instance, node.Result));
                Assert.Equal(2, node.Executions);
            }
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record Options(int Offset);

    private sealed class OffsetNode : AbstractNode {
        public IInputPort<int> InputValue { get; }
        public IOutputPort<int> Result { get; }
        public int Executions { get; private set; }

        public OffsetNode() {
            UseCache = true;
            InputValue = Input(new NumericPortBuilder<int>(nameof(InputValue)).Input());
            Result = Output(new NumericPortBuilder<int>(nameof(Result)).Output());
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Executions++;
            Result.Write(context, InputValue.Read(context) + context.GetOptions<Options>()!.Offset);
            return ValueTask.FromResult(true);
        }
    }
}
