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
| &#39;Simple | System&#39;     |  0.9248 ns |  0.3287 ns | 0.0180 ns |     1.00x |  0.62x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |  1.5035 ns |  2.2530 ns | 0.1235 ns |     1.63x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |  1.3591 ns |  2.2444 ns | 0.1230 ns |     1.47x |  0.90x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |  0.9001 ns |  2.6865 ns | 0.1473 ns |     1.00x |  0.63x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |  1.4379 ns |  1.8169 ns | 0.0996 ns |     1.60x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |  1.6130 ns |  1.1791 ns | 0.0646 ns |     1.79x |  1.12x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |  1.1478 ns |  1.1516 ns | 0.0631 ns |     1.00x |  0.73x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |  1.5769 ns |  2.5526 ns | 0.1399 ns |     1.37x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |  1.7474 ns |  1.0652 ns | 0.0584 ns |     1.52x |  1.11x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | 53.1399 ns | 13.6193 ns | 0.7465 ns |     1.00x |  0.98x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | 54.3445 ns | 32.0415 ns | 1.7563 ns |     1.02x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | 54.1479 ns | 18.1129 ns | 0.9928 ns |     1.02x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 83.0859 ns | 14.0211 ns | 0.7685 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Loop | FEC&#39;          |         NA |         NA |        NA |         ? |      ? |               ? |            ? |     NA |        NA |
| &#39;Loop | Hyperbee&#39;     | 84.7570 ns |  4.3596 ns | 0.2390 ns |     1.02x |      ? |           1.00x |            ? |      - |         - |
| &#39;Switch | System&#39;     |  4.5228 ns |  3.8419 ns | 0.2106 ns |     1.00x |  1.35x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |  3.3384 ns |  2.3622 ns | 0.1295 ns |     0.74x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |  3.5953 ns |  1.3379 ns | 0.0733 ns |     0.79x |  1.08x |           1.00x |        1.00x |      - |         - |

Benchmarks with issues:
  ExecutionBenchmarks.'Loop | FEC': .NET 9(Runtime=.NET 9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
