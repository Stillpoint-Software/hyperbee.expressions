```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                                               | Mean | Error | vs System | vs Fec | Alloc vs System | Alloc vs Fec |
|----------------------------------------------------- |-----:|------:|----------:|-------:|----------------:|-------------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     |   NA |    NA |       N/A |    N/A |             N/A |          N/A |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; |   NA |    NA |       N/A |    N/A |             N/A |          N/A |

Benchmarks with issues:
  CoroutineExecutionBenchmarks.'AsyncSuspending x16 | Hyperbee': .NET 9(Runtime=.NET 9.0, IterationCount=20, LaunchCount=1, WarmupCount=8)
  CoroutineExecutionBenchmarks.'AsyncSuspending x16 (delegate MoveNext) | Hyperbee': .NET 9(Runtime=.NET 9.0, IterationCount=20, LaunchCount=1, WarmupCount=8)
