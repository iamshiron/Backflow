# Shiron.Backflow

[![License](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)
[![CI](https://github.com/iamshiron/Backflow/actions/workflows/ci.yml/badge.svg)](https://github.com/iamshiron/Backflow/actions/workflows/ci.yml)
[![Code Quality](https://github.com/iamshiron/Backflow/actions/workflows/code-quality.yml/badge.svg)](https://github.com/iamshiron/Backflow/actions/workflows/code-quality.yml)

A .NET 10 DAG-based data pipeline framework. Workflows are graphs of nodes connected by typed ports; execution order comes from a topological sort, and data flows through a shared array-backed memory bus keyed by integer channels.

## Data flow

```
BUILD ─ PipelineBuilder

  AddNode(node)          assigns each port a channel ID (int):
                          add.A ─► 0   add.B ─► 1   add.Sum ─► 2
  AddConnection(a.Sum,   aliases the destination input channel onto
    print.Msg)           the source output channel — they share one
                         storage slot (print.Msg remapped onto 2)
  Build()                validates type compat + cycle-freeness,
                         resolves generic type args → Pipeline

CONTEXT ─ ArrayPipelineContext (one shared ArrayBucketStore)

  add.A      ─►  channel 0   (int)
  add.B      ─►  channel 1   (int)
  add.Sum    ─►  channel 2   (int)
  print.Msg  ─►  channel 2   (obj)     ← same slot as add.Sum

  Read<T> when T differs from the channel's declared type:
    exact match → assignability → CastRegistry rule   ("cast-on-read")

EXECUTE ─ PipelineExecutor

  layers come from a topological sort; nodes within a layer have no
  data dependency on each other and run in parallel (ExecuteAsync):

    layer 0:  [ AddNode ]
    layer 1:  [ PrintNode ]
```

Connected ports alias a single channel — a write to an output is instantly visible to its input. There are no queues or messages. The channel's declared type is the source (output) port's type; if a destination reads in a different type, the `CastRegistry` converts on read.

## Quick start

```csharp
using Shiron.Backflow;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port.Builder;

class AddNode : AbstractNode {
    public IInputPort<int> A = null!;
    public IInputPort<int> B = null!;
    public IOutputPort<int> Sum = null!;

    public AddNode() {
        A = Input(new NumericPortBuilder<int>(nameof(A)).Input());
        B = Input(new NumericPortBuilder<int>(nameof(B)).Input());
        Sum = Output(new NumericPortBuilder<int>(nameof(Sum)).Output());
    }

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Sum.Write(context, A.Read(context) + B.Read(context));
        return ValueTask.FromResult(true);
    }
}

// 1. Register node types
var registry = new NodeRegistry();
var add = registry.Register<AddNode>();
var print = registry.Register<PrintNode>();

// 2. Build the graph (assigns channels, validates types + cycle-freeness)
var builder = new PipelineBuilder(registry);
var addInst = builder.AddNode(add);
var printInst = builder.AddNode(print);
builder.AddConnection(addInst, add.Sum, printInst, print.Message);
var pipeline = builder.Build();

// 3. Create the shared context, seed inputs
var ctx = ArrayPipelineContext.ForPipeline(pipeline, builder.CastRegistry);
ctx.Write(addInst, add.A, 19);
ctx.Write(addInst, add.B, 95);

// 4. Execute (layers run in parallel with ExecuteAsync)
var stats = new PipelineExecutor(pipeline).Execute(ctx);
```

A node derives from `AbstractNode`, declares ports via fluent builders in its constructor, and implements `ExecuteNodeAsync(INodeContext)`. Port builders (`NumericPortBuilder<T>`, `StringPortBuilder`, `ArrayPortBuilder<T>`, `EnumPortBuilder<T>`, `BlobPortBuilder<T>`, `AnyPortBuilder`, ...) attach validators that run fail-fast on every read.

Beyond the basics, the framework supports: open-generic nodes with type inference from connections, lifecycle `INodeBehavior`s (e.g. `ChipEnableBehavior`, `EnableOutBehavior`), array ports with indexed connections, optional output caching (per-node `UseCache`) with blob offloading, `IPipelineEventListener` hooks, and JSON round-trip serialization of topologies and inputs. See [`samples/Backflow/`](samples/Backflow/) for runnable examples.

## Installation

```bash
git submodule add <repository-url> Backflow
git submodule update --init --recursive
```

```xml
<ItemGroup>
  <ProjectReference Include="..\Backflow\src\Backflow\Shiron.Backflow.csproj" />
</ItemGroup>
```

## Dependencies

- [Shiron.Lib](https://github.com/iamshiron/lib) — git submodule at `external/lib`; provides `DirectedAcyclicGraph<T>` and `ArrayBucketStore`.

## License

GNU Affero General Public License v3.0 (AGPL-3.0)
