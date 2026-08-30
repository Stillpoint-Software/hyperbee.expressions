```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                | Mean        | Error       | StdDev     | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|---------------------- |------------:|------------:|-----------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;Simple | System&#39;     |   0.9952 ns |   0.1023 ns |  0.0056 ns |     1.00x |  0.55x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |   1.8236 ns |   3.0859 ns |  0.1691 ns |     1.83x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |   2.9243 ns |   0.7022 ns |  0.0385 ns |     2.94x |  1.60x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |   0.7346 ns |   0.9486 ns |  0.0520 ns |     1.00x |  0.37x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |   1.9747 ns |   0.2553 ns |  0.0140 ns |     2.69x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |   1.4488 ns |   1.9227 ns |  0.1054 ns |     1.97x |  0.73x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |   1.2215 ns |   3.2681 ns |  0.1791 ns |     1.00x |  0.65x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |   1.8661 ns |   0.8384 ns |  0.0460 ns |     1.53x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |   3.8929 ns |   3.1855 ns |  0.1746 ns |     3.19x |  2.09x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    |  60.7565 ns |  12.8232 ns |  0.7029 ns |     1.00x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       |  60.7251 ns |  34.6706 ns |  1.9004 ns |     1.00x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  |  63.4309 ns |  33.9421 ns |  1.8605 ns |     1.04x |  1.04x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 114.0826 ns | 210.7310 ns | 11.5509 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Loop | FEC&#39;          |          NA |          NA |         NA |         ? |      ? |               ? |            ? |     NA |        NA |
| &#39;Loop | Hyperbee&#39;     | 101.0355 ns |  73.4170 ns |  4.0242 ns |     0.89x |      ? |           1.00x |            ? |      - |         - |
| &#39;Switch | System&#39;     |   2.8385 ns |   2.1431 ns |  0.1175 ns |     1.00x |  0.93x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |   3.0456 ns |   3.2043 ns |  0.1756 ns |     1.07x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |   4.3798 ns |   3.7463 ns |  0.2053 ns |     1.54x |  1.44x |           1.00x |        1.00x |      - |         - |

Benchmarks with issues:
  ExecutionBenchmarks.'Loop | FEC': .NET 9(Runtime=.NET 9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
