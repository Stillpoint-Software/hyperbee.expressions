```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                | Mean       | Error      | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|---------------------- |-----------:|-----------:|----------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;Simple | System&#39;     |  1.0023 ns |  0.4127 ns | 0.0226 ns |     1.00x |  0.61x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |  1.6383 ns |  0.9461 ns | 0.0519 ns |     1.63x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |  1.6658 ns |  2.7335 ns | 0.1498 ns |     1.66x |  1.02x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |  0.7548 ns |  3.1174 ns | 0.1709 ns |     1.00x |  0.49x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |  1.5257 ns |  0.3446 ns | 0.0189 ns |     2.02x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |  2.6247 ns |  5.6983 ns | 0.3123 ns |     3.48x |  1.72x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |  1.3261 ns |  1.7659 ns | 0.0968 ns |     1.00x |  0.85x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |  1.5568 ns |  3.4422 ns | 0.1887 ns |     1.17x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |  1.9072 ns |  8.6630 ns | 0.4748 ns |     1.44x |  1.23x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | 53.5944 ns | 32.1675 ns | 1.7632 ns |     1.00x |  0.97x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | 55.0971 ns | 21.0642 ns | 1.1546 ns |     1.03x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | 58.0640 ns | 68.9871 ns | 3.7814 ns |     1.08x |  1.05x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 82.8377 ns | 47.2441 ns | 2.5896 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Loop | FEC&#39;          |         NA |         NA |        NA |         ? |      ? |               ? |            ? |     NA |        NA |
| &#39;Loop | Hyperbee&#39;     | 99.5739 ns | 40.8200 ns | 2.2375 ns |     1.20x |      ? |           1.00x |            ? |      - |         - |
| &#39;Switch | System&#39;     |  2.7019 ns |  4.8790 ns | 0.2674 ns |     1.00x |  0.91x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |  2.9774 ns |  4.9788 ns | 0.2729 ns |     1.10x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |  4.1874 ns |  3.5818 ns | 0.1963 ns |     1.55x |  1.41x |           1.00x |        1.00x |      - |         - |

Benchmarks with issues:
  ExecutionBenchmarks.'Loop | FEC': .NET 9(Runtime=.NET 9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
