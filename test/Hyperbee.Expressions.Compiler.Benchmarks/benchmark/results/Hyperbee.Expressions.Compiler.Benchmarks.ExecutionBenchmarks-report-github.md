```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7922/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                | Mean       | Error      | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|---------------------- |-----------:|-----------:|----------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;Simple | System&#39;     |  0.4919 ns |  0.4883 ns | 0.0268 ns |     1.00x |  0.51x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |  0.9665 ns |  1.3302 ns | 0.0729 ns |     1.96x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |  1.4092 ns |  1.5003 ns | 0.0822 ns |     2.86x |  1.46x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |  0.7753 ns |  0.4714 ns | 0.0258 ns |     1.00x |  0.66x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |  1.1782 ns |  2.0251 ns | 0.1110 ns |     1.52x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |  1.8758 ns |  4.5519 ns | 0.2495 ns |     2.42x |  1.59x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |  0.4437 ns |  1.3286 ns | 0.0728 ns |     1.00x |  0.44x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |  0.9998 ns |  0.9089 ns | 0.0498 ns |     2.25x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |  1.5717 ns |  1.9668 ns | 0.1078 ns |     3.54x |  1.57x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | 27.4314 ns | 61.9934 ns | 3.3981 ns |     1.00x |  1.08x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | 25.4080 ns | 17.4671 ns | 0.9574 ns |     0.93x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | 24.3183 ns | 29.0727 ns | 1.5936 ns |     0.89x |  0.96x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 30.8853 ns | 22.0935 ns | 1.2110 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Loop | FEC&#39;          |         NA |         NA |        NA |         ? |      ? |               ? |            ? |     NA |        NA |
| &#39;Loop | Hyperbee&#39;     | 30.3674 ns |  2.8324 ns | 0.1553 ns |     0.98x |      ? |           1.00x |            ? |      - |         - |
| &#39;Switch | System&#39;     |  1.4905 ns |  1.1938 ns | 0.0654 ns |     1.00x |  0.95x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |  1.5648 ns |  0.8382 ns | 0.0459 ns |     1.05x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |  2.0122 ns |  3.3828 ns | 0.1854 ns |     1.35x |  1.29x |           1.00x |        1.00x |      - |         - |

Benchmarks with issues:
  ExecutionBenchmarks.'Loop | FEC': .NET 9(Runtime=.NET 9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
