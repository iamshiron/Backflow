# Shiron.Backflow

[![License](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)
[![CI](https://github.com/iamshiron/Backflow/actions/workflows/ci.yml/badge.svg)](https://github.com/iamshiron/Backflow/actions/workflows/ci.yml)
[![Code Quality](https://github.com/iamshiron/Backflow/actions/workflows/code-quality.yml/badge.svg)](https://github.com/iamshiron/Backflow/actions/workflows/code-quality.yml)

A .NET 10 DAG-based data pipeline framework with a strongly-typed port system, generic node support, and type-safe implicit casting.

## Overview

Backflow lets you build composable, type-safe data transformation workflows as directed acyclic graphs. Nodes process data and pass results to downstream nodes via typed ports, with automatic execution ordering via topological sort.

### Key features

- **Typed ports** with fluent builders and fail-fast validators (numeric ranges, string lengths, enum constraints)
- **Generic nodes** with automatic type inference from connections — `GenericAddNode<T>` resolves to `GenericAddNode<int>` when connected to an int output
- **Type casting** — implicit numeric casts (lossless/lossy), built-in ToString fallback, custom domain casts via `RegisterCast`, strict mode to reject lossy conversions
- **Array ports** — `IArrayInputPort<T>` with frozen count and indexed connections, `IArrayOutputPort<T>` with element type metadata
- **Serialization** — pipeline definitions and runtime state round-trip through JSON
- **Node behaviors** — extensible `INodeBehavior` lifecycle (e.g., `ChipEnableBehavior`, `EnableOutBehavior`)
- **Layer-parallel execution** — topological sorting creates execution layers where nodes within a layer can run in parallel
- **Deterministic caching** — cache keys computed from node type and input values
- **Blob storage** — automatic offloading of large data to pluggable blob storage

## Quick start

```csharp
using Shiron.Backflow;
using Shiron.Backflow.Casting;
using Shiron.Backflow.Context;
using Shiron.Backflow.Node;
using Shiron.Backflow.Port.Builder;

// Define nodes with typed ports
class AddNode : AbstractNode {
    public readonly IInputPort<int> A = Input(new NumericPortBuilder<int>("A").Input());
    public readonly IInputPort<int> B = Input(new NumericPortBuilder<int>("B").Input());
    public readonly IOutputPort<int> Sum = Output(new NumericPortBuilder<int>("Sum").Output());

    protected override ValueTask<bool> ExecuteNodeAsync(INodeContext context) {
        Sum.Write(context, A.Read(context) + B.Read(context));
        return ValueTask.FromResult(true);
    }
}

// Register and build
var registry = new NodeRegistry();
var addNode = registry.Register<AddNode>();

var builder = new PipelineBuilder(registry)
    .RegisterCast<string, int>(TypeCast.Lossy, s => int.Parse(s));

var add = builder.AddNode(addNode);
var consumer = builder.AddNode(new PrintNode());

builder.AddConnection(add, add.Sum, consumer, consumer.Message);

var pipeline = builder.Build();
var ctx = builder.CreateContext();

// Execute
var executor = new PipelineExecutor(pipeline);
var stats = executor.Execute(ctx);
```

See [`samples/Backflow/`](samples/Backflow/) for comprehensive examples including generic nodes, implicit casting, array ports, custom types, and serialization.

## Dependencies

- [Shiron.Lib](https://github.com/iamshiron/lib) — included as a git submodule at `external/lib` for the `Collections` module (`DirectedAcyclicGraph<T>`, `ArrayBucketStore`)

## Getting started

Add Backflow as a git submodule:

```bash
git submodule add <repository-url> Backflow
git submodule update --init --recursive
```

Then reference the projects you need in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Backflow\src\Backflow\Shiron.Backflow.csproj" />
</ItemGroup>
```

All projects target .NET 10.

## License

GNU Affero General Public License v3.0 (AGPL-3.0)
