```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                | Mean        | Error      | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|---------------------- |------------:|-----------:|----------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;Simple | System&#39;     |   1.3232 ns |  3.4006 ns | 0.1864 ns |     1.00x |  0.60x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |   2.2026 ns |  1.2275 ns | 0.0673 ns |     1.66x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |   2.9254 ns |  5.3597 ns | 0.2938 ns |     2.21x |  1.33x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |   0.7497 ns |  3.6215 ns | 0.1985 ns |     1.00x |  0.48x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |   1.5475 ns |  3.2059 ns | 0.1757 ns |     2.06x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |   2.3623 ns |  3.9724 ns | 0.2177 ns |     3.15x |  1.53x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |   1.6827 ns |  2.2529 ns | 0.1235 ns |     1.00x |  1.32x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |   1.2794 ns |  1.0698 ns | 0.0586 ns |     0.76x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |   3.9933 ns |  2.2731 ns | 0.1246 ns |     2.37x |  3.12x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    |  63.8039 ns | 40.0952 ns | 2.1978 ns |     1.00x |  0.82x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       |  78.2700 ns | 83.3787 ns | 4.5703 ns |     1.23x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  |  83.0758 ns | 50.4842 ns | 2.7672 ns |     1.30x |  1.06x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 107.0231 ns | 56.6081 ns | 3.1029 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Loop | FEC&#39;          |          NA |         NA |        NA |         ? |      ? |               ? |            ? |     NA |        NA |
| &#39;Loop | Hyperbee&#39;     |  98.8310 ns | 15.5887 ns | 0.8545 ns |     0.92x |      ? |           1.00x |            ? |      - |         - |
| &#39;Switch | System&#39;     |   2.8274 ns |  3.3140 ns | 0.1817 ns |     1.00x |  0.95x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |   2.9702 ns |  1.6999 ns | 0.0932 ns |     1.05x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |   4.2103 ns |  1.9126 ns | 0.1048 ns |     1.49x |  1.42x |           1.00x |        1.00x |      - |         - |

Benchmarks with issues:
  ExecutionBenchmarks.'Loop | FEC': .NET 9(Runtime=.NET 9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
