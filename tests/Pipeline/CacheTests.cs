using Shiron.Lib.Pipeline;
using Shiron.Lib.Pipeline.Caching;
using Shiron.Lib.Pipeline.Context;
using Shiron.Lib.Pipeline.Node;
using Shiron.Lib.Pipeline.Port;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shiron.Lib.Pipeline.Registry;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class CacheKeyFactoryTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class AddNode : AbstractNode {
        public readonly IInputPort<int> A;
        public readonly IInputPort<int> B;
        public readonly IOutputPort<int> Sum;

        public AddNode() {
            A = Input(new InputPort<int>("a", 0, new PassValidator<int>()));
            B = Input(new InputPort<int>("b", 0, new PassValidator<int>()));
            Sum = Output(new OutputPort<int>("sum"));
            UseCache = true;
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Sum.Write(context, A.Read(context) + B.Read(context));
            return ValueTask.FromResult(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void SameInputs_ProduceSameKey() {
        var builder = new PipelineBuilder(_registry);
        var node = new AddNode();
        var inst = builder.AddNode(node);
        var ctx = new PipelineContext();
        ctx.Write(inst, node.A, 10);
        ctx.Write(inst, node.B, 20);

        var factory = new CacheKeyFactory();
        var key1 = factory.CreateKey(inst, ctx);
        var key2 = factory.CreateKey(inst, ctx);

        Assert.Equal(key1.CombinedHash, key2.CombinedHash);
    }

    [Fact]
    public void DifferentInputs_ProduceDifferentKeys() {
        var builder = new PipelineBuilder(_registry);
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.A, 10);
        ctx1.Write(inst, node.B, 20);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.A, 99);
        ctx2.Write(inst, node.B, 1);

        var factory = new CacheKeyFactory();
        var key1 = factory.CreateKey(inst, ctx1);
        var key2 = factory.CreateKey(inst, ctx2);

        Assert.NotEqual(key1.CombinedHash, key2.CombinedHash);
    }

    [Fact]
    public void SameInputValues_ProduceSameKey_RegardlessOfPortWriteOrder() {
        var builder = new PipelineBuilder(_registry);
        var node = new AddNode();
        var inst = builder.AddNode(node);

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.A, 5);
        ctx1.Write(inst, node.B, 15);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.B, 15);
        ctx2.Write(inst, node.A, 5);

        var factory = new CacheKeyFactory();
        var key1 = factory.CreateKey(inst, ctx1);
        var key2 = factory.CreateKey(inst, ctx2);

        Assert.Equal(key1.CombinedHash, key2.CombinedHash);
    }

    [Fact]
    public void DifferentNodeTypes_ProduceDifferentKeys() {
        var builder = new PipelineBuilder(_registry);

        var node1 = new AddNode();
        var inst1 = builder.AddNode(node1);

        var node2 = new MultiplyNode();
        var inst2 = builder.AddNode(node2);

        var ctx1 = new PipelineContext();
        ctx1.Write(inst1, node1.A, 10);
        ctx1.Write(inst1, node1.B, 20);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst2, node2.A, 10);
        ctx2.Write(inst2, node2.B, 20);

        var factory = new CacheKeyFactory();
        var key1 = factory.CreateKey(inst1, ctx1);
        var key2 = factory.CreateKey(inst2, ctx2);

        Assert.NotEqual(key1.CombinedHash, key2.CombinedHash);
    }

    private class MultiplyNode : AbstractNode {
        public readonly IInputPort<int> A;
        public readonly IInputPort<int> B;
        public readonly IOutputPort<int> Product;

        public MultiplyNode() {
            A = Input(new InputPort<int>("a", 0, new PassValidator<int>()));
            B = Input(new InputPort<int>("b", 0, new PassValidator<int>()));
            Product = Output(new OutputPort<int>("product"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Product.Write(context, A.Read(context) * B.Read(context));
            return ValueTask.FromResult(true);
        }
    }
}

public class CacheExecutionTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private class SourceNode : AbstractNode {
        public readonly IOutputPort<int> Out;

        public SourceNode() {
            Out = Output(new OutputPort<int>("out"));
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            return ValueTask.FromResult(true);
        }
    }

    private class ComputeNode : AbstractNode {
        public readonly IInputPort<int> In;
        public readonly IOutputPort<int> Out;
        public int ExecutionCount;

        public ComputeNode() {
            In = Input(new InputPort<int>("in", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<int>("out"));
            UseCache = true;
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            ExecutionCount++;
            Out.Write(context, In.Read(context) * 2);
            return ValueTask.FromResult(true);
        }
    }

    private class ComputeStringNode : AbstractNode {
        public readonly IInputPort<string> In;
        public readonly IOutputPort<string> Out;
        public int ExecutionCount;

        public ComputeStringNode() {
            In = Input(new InputPort<string>("in", "", new PassValidator<string>()));
            Out = Output(new OutputPort<string>("out"));
            UseCache = true;
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            ExecutionCount++;
            Out.Write(context, In.Read(context)!.ToUpperInvariant());
            return ValueTask.FromResult(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void Execute_CacheHit_SkipsNodeExecution() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new ComputeNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.In, 21);
        new PipelineExecutor(pipeline, cache).Execute(ctx1);
        Assert.Equal(1, node.ExecutionCount);
        Assert.Equal(42, ctx1.Read<int>(inst, node.Out));

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.In, 21);
        new PipelineExecutor(pipeline, cache).Execute(ctx2);
        Assert.Equal(1, node.ExecutionCount);
        Assert.Equal(42, ctx2.Read<int>(inst, node.Out));
    }

    [Fact]
    public async Task ExecuteAsync_CacheHit_SkipsNodeExecution() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new ComputeNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.In, 21);
        await new PipelineExecutor(pipeline, cache).ExecuteAsync(ctx1);
        Assert.Equal(1, node.ExecutionCount);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.In, 21);
        await new PipelineExecutor(pipeline, cache).ExecuteAsync(ctx2);
        Assert.Equal(1, node.ExecutionCount);
        Assert.Equal(42, ctx2.Read<int>(inst, node.Out));
    }

    [Fact]
    public void Execute_DifferentInputs_InvalidatesCache() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new ComputeNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.In, 21);
        new PipelineExecutor(pipeline, cache).Execute(ctx1);
        Assert.Equal(1, node.ExecutionCount);
        Assert.Equal(42, ctx1.Read<int>(inst, node.Out));

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.In, 10);
        new PipelineExecutor(pipeline, cache).Execute(ctx2);
        Assert.Equal(2, node.ExecutionCount);
        Assert.Equal(20, ctx2.Read<int>(inst, node.Out));
    }

    [Fact]
    public void Execute_Stats_TrackHitsAndMisses() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new ComputeNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.In, 21);
        var stats1 = new PipelineExecutor(pipeline, cache).Execute(ctx1);
        Assert.Equal(0, stats1.CacheHits);
        Assert.Equal(1, stats1.CacheMisses);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.In, 21);
        var stats2 = new PipelineExecutor(pipeline, cache).Execute(ctx2);
        Assert.Equal(1, stats2.CacheHits);
        Assert.Equal(0, stats2.CacheMisses);
    }

    [Fact]
    public void Execute_NoCache_DoesNotAffectStats() {
        var builder = new PipelineBuilder(_registry);
        var node = new ComputeNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx = new PipelineContext();
        ctx.Write(inst, node.In, 21);
        var stats = new PipelineExecutor(pipeline).Execute(ctx);

        Assert.Equal(0, stats.CacheHits);
        Assert.Equal(0, stats.CacheMisses);
        Assert.Equal(1, node.ExecutionCount);
    }

    [Fact]
    public void Execute_NodeWithUseCacheFalse_IsNotCached() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new SourceNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        new PipelineExecutor(pipeline, cache).Execute(ctx1);

        var ctx2 = new PipelineContext();
        var stats = new PipelineExecutor(pipeline, cache).Execute(ctx2);

        Assert.Equal(0, stats.CacheHits);
        Assert.Equal(0, stats.CacheMisses);
    }

    [Fact]
    public void Execute_PipelineWithMultipleNodes_CachesIndependently() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node1 = new ComputeNode();
        var node2 = new ComputeStringNode();
        var inst1 = builder.AddNode(node1);
        var inst2 = builder.AddNode(node2);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst1, node1.In, 5);
        ctx1.Write(inst2, node2.In, "hello");
        var stats1 = new PipelineExecutor(pipeline, cache).Execute(ctx1);
        Assert.Equal(0, stats1.CacheHits);
        Assert.Equal(2, stats1.CacheMisses);
        Assert.Equal(1, node1.ExecutionCount);
        Assert.Equal(1, node2.ExecutionCount);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst1, node1.In, 5);
        ctx2.Write(inst2, node2.In, "world");
        var stats2 = new PipelineExecutor(pipeline, cache).Execute(ctx2);
        Assert.Equal(1, stats2.CacheHits);
        Assert.Equal(1, stats2.CacheMisses);
        Assert.Equal(1, node1.ExecutionCount);
        Assert.Equal(2, node2.ExecutionCount);
    }

    [Fact]
    public void Execute_CacheHit_RestoresCorrectOutputTypes() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new ComputeStringNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.In, "test");
        new PipelineExecutor(pipeline, cache).Execute(ctx1);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.In, "test");
        new PipelineExecutor(pipeline, cache).Execute(ctx2);

        var result = ctx2.Read<string>(inst, node.Out);
        Assert.Equal("TEST", result);
    }

    [Fact]
    public void Execute_ChangeInputBackAndForth_CacheHitsAndMissesAlternately() {
        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        var builder = new PipelineBuilder(_registry);
        var node = new ComputeNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctxA1 = new PipelineContext();
        ctxA1.Write(inst, node.In, 10);
        var statsA1 = new PipelineExecutor(pipeline, cache).Execute(ctxA1);
        Assert.Equal(0, statsA1.CacheHits);
        Assert.Equal(1, statsA1.CacheMisses);
        Assert.Equal(1, node.ExecutionCount);

        var ctxB = new PipelineContext();
        ctxB.Write(inst, node.In, 20);
        var statsB = new PipelineExecutor(pipeline, cache).Execute(ctxB);
        Assert.Equal(0, statsB.CacheHits);
        Assert.Equal(1, statsB.CacheMisses);
        Assert.Equal(2, node.ExecutionCount);

        var ctxA2 = new PipelineContext();
        ctxA2.Write(inst, node.In, 10);
        var statsA2 = new PipelineExecutor(pipeline, cache).Execute(ctxA2);
        Assert.Equal(1, statsA2.CacheHits);
        Assert.Equal(0, statsA2.CacheMisses);
        Assert.Equal(2, node.ExecutionCount);
        Assert.Equal(20, ctxA2.Read<int>(inst, node.Out));
    }
}

public class CacheTypeAdapterTests {
    private class PassValidator<T> : IPortValidator<T> {
        public string? Validate(T? value) => null;
    }

    private readonly record struct Point<T>(T X, T Y);

    private class PointJsonConverter<T> : JsonConverter<Point<T>> {
        public override Point<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            return new Point<T>(
                root.GetProperty("X").Deserialize<T>(options)!,
                root.GetProperty("Y").Deserialize<T>(options)!
            );
        }

        public override void Write(Utf8JsonWriter writer, Point<T> value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            writer.WritePropertyName("X");
            JsonSerializer.Serialize(writer, value.X, options);
            writer.WritePropertyName("Y");
            JsonSerializer.Serialize(writer, value.Y, options);
            writer.WriteEndObject();
        }
    }

    private class PointNode : AbstractNode {
        public readonly IInputPort<int> X;
        public readonly IInputPort<int> Y;
        public readonly IOutputPort<Point<int>> Out;

        public PointNode() {
            X = Input(new InputPort<int>("x", 0, new PassValidator<int>()));
            Y = Input(new InputPort<int>("y", 0, new PassValidator<int>()));
            Out = Output(new OutputPort<Point<int>>("out"));
            UseCache = true;
        }

        protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
            Out.Write(context, new Point<int>(X.Read(context), Y.Read(context)));
            return ValueTask.FromResult(true);
        }
    }

    private readonly NodeRegistry _registry = new();

    [Fact]
    public void Execute_WithOpenGenericConverter_CacheHitsOnSameInputs() {
        var adapters = new CacheTypeAdapterRegistry();
        adapters.Register(typeof(PointJsonConverter<>));

        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"), adapters);

        var builder = new PipelineBuilder(_registry);
        var node = new PointNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.X, 10);
        ctx1.Write(inst, node.Y, 20);
        var stats1 = new PipelineExecutor(pipeline, cache, typeAdapters: adapters).Execute(ctx1);
        Assert.Equal(0, stats1.CacheHits);
        Assert.Equal(1, stats1.CacheMisses);
        Assert.Equal(new Point<int>(10, 20), ctx1.Read<Point<int>>(inst, node.Out));

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.X, 10);
        ctx2.Write(inst, node.Y, 20);
        var stats2 = new PipelineExecutor(pipeline, cache, typeAdapters: adapters).Execute(ctx2);
        Assert.Equal(1, stats2.CacheHits);
        Assert.Equal(0, stats2.CacheMisses);
        Assert.Equal(new Point<int>(10, 20), ctx2.Read<Point<int>>(inst, node.Out));
    }

    [Fact]
    public void Execute_WithOpenGenericConverter_DifferentInputsInvalidatesCache() {
        var adapters = new CacheTypeAdapterRegistry();
        adapters.Register(typeof(PointJsonConverter<>));

        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"), adapters);

        var builder = new PipelineBuilder(_registry);
        var node = new PointNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.X, 10);
        ctx1.Write(inst, node.Y, 20);
        new PipelineExecutor(pipeline, cache, typeAdapters: adapters).Execute(ctx1);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.X, 30);
        ctx2.Write(inst, node.Y, 40);
        var stats2 = new PipelineExecutor(pipeline, cache, typeAdapters: adapters).Execute(ctx2);
        Assert.Equal(0, stats2.CacheHits);
        Assert.Equal(1, stats2.CacheMisses);
        Assert.Equal(new Point<int>(30, 40), ctx2.Read<Point<int>>(inst, node.Out));
    }

    [Fact]
    public void Execute_WithConcreteConverter_CacheHitsOnSameInputs() {
        var adapters = new CacheTypeAdapterRegistry();
        adapters.Register(new PointJsonConverter<int>());

        using var cache = new JsonFileCache(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"), adapters);

        var builder = new PipelineBuilder(_registry);
        var node = new PointNode();
        var inst = builder.AddNode(node);

        var pipeline = builder.Build();

        var ctx1 = new PipelineContext();
        ctx1.Write(inst, node.X, 10);
        ctx1.Write(inst, node.Y, 20);
        new PipelineExecutor(pipeline, cache, typeAdapters: adapters).Execute(ctx1);

        var ctx2 = new PipelineContext();
        ctx2.Write(inst, node.X, 10);
        ctx2.Write(inst, node.Y, 20);
        var stats2 = new PipelineExecutor(pipeline, cache, typeAdapters: adapters).Execute(ctx2);
        Assert.Equal(1, stats2.CacheHits);
        Assert.Equal(0, stats2.CacheMisses);
        Assert.Equal(new Point<int>(10, 20), ctx2.Read<Point<int>>(inst, node.Out));
    }
}
