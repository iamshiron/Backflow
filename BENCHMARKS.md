# Reusable execution measurements

Measured 2026-10-30 with BenchmarkDotNet 0.15.8, .NET SDK 10.0.112 / runtime 10.0.12,
Linux x64, AMD Ryzen AI 9 365. Short job: one launch, three warmups, three measured iterations.
The host was shared with other development tasks; timing confidence intervals are wide. These are
local measurements, not throughput guarantees or before/after comparisons.

```sh
dotnet run --configuration Release --project benchmarks/Backflow/Shiron.Backflow.Benchmarks.csproj -- \
  --filter '*ReusableExecutionBenchmarks*' --job short --warmupCount 3 --iterationCount 3
```

The benchmark builds a serial graph from one shared stateless integer-copy definition, creates one
context, and warms its execution adapters in `GlobalSetup`. Each measured operation executes the
whole graph and clears previous output channels. Graph compilation, context creation, caching,
listeners, array ports, and genuinely asynchronous nodes are deliberately excluded.

| Nodes | API | Mean | Standard deviation | Managed allocation/run |
|---:|---|---:|---:|---:|
| 1 | `Execute` | 276.1 ns | 24.49 ns | 0 B |
| 1 | `ExecuteValueAsync` | 277.1 ns | 7.75 ns | 0 B |
| 1 | `ExecuteAsync` | 303.4 ns | 55.90 ns | 96 B |
| 32 | `Execute` | 7.491 µs | 0.099 µs | 0 B |
| 32 | `ExecuteValueAsync` | 8.076 µs | 0.079 µs | 0 B |
| 32 | `ExecuteAsync` | 7.559 µs | 0.017 µs | 96 B |
| 256 | `Execute` | 68.331 µs | 8.731 µs | 0 B |
| 256 | `ExecuteValueAsync` | 81.437 µs | 2.202 µs | 0 B |
| 256 | `ExecuteAsync` | 58.325 µs | 0.647 µs | 96 B |

`ReusableExecutionTests.ExecuteValueAsync_WarmedSynchronousTypedNodes_DoesNotAllocate` also measures
1,000 executions after 1,000 warmups using `GC.GetAllocatedBytesForCurrentThread` for each graph size:
zero bytes across the measured loop. `Read_NumericCastOnRead_DoesNotBox` independently checks 1,000
typed int-to-double reads with zero managed allocations. These narrow assertions do not cover
untyped values, cache serialization, event objects, array assembly or asynchronous state machines.

Allocation-removing changes include context-owned reusable node adapters/state arrays, compiled
channel layout/indexed connections, synchronous `ValueTask` completion, timestamp-based timing,
typed numeric conversion reads, and removing per-node thread-pool dispatch. Initial setup still
allocates and retains context-local buffers; genuinely asynchronous work allocates pending tasks
as required to join all outstanding operations safely.
