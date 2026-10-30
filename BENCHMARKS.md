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
| 1 | `Execute` | 251.2 ns | 1.43 ns | 0 B |
| 1 | `ExecuteValueAsync` | 263.3 ns | 10.22 ns | 0 B |
| 1 | `ExecuteAsync` | 265.5 ns | 1.26 ns | 96 B |
| 32 | `Execute` | 7.801 µs | 0.254 µs | 0 B |
| 32 | `ExecuteValueAsync` | 10.448 µs | 0.538 µs | 0 B |
| 32 | `ExecuteAsync` | 11.379 µs | 0.483 µs | 96 B |
| 256 | `Execute` | 61.235 µs | 0.359 µs | 0 B |
| 256 | `ExecuteValueAsync` | 74.465 µs | 8.827 µs | 0 B |
| 256 | `ExecuteAsync` | 61.666 µs | 0.705 µs | 96 B |

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
