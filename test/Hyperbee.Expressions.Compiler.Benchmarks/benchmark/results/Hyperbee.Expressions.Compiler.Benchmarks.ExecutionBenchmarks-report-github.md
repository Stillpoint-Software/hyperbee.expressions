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
| &#39;Simple | System&#39;     |  0.7742 ns |  0.5485 ns | 0.0301 ns |     1.00x |  0.47x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |  1.6557 ns |  0.8221 ns | 0.0451 ns |     2.14x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |  2.3852 ns |  2.3069 ns | 0.1265 ns |     3.08x |  1.44x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |  0.6748 ns |  0.9167 ns | 0.0502 ns |     1.00x |  0.55x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |  1.2205 ns |  2.1195 ns | 0.1162 ns |     1.81x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |  1.5505 ns |  0.4577 ns | 0.0251 ns |     2.30x |  1.27x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |  1.5420 ns |  1.2285 ns | 0.0673 ns |     1.00x |  1.02x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |  1.5089 ns |  1.3373 ns | 0.0733 ns |     0.98x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |  3.5719 ns |  3.3352 ns | 0.1828 ns |     2.32x |  2.37x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | 55.3741 ns | 23.3713 ns | 1.2811 ns |     1.00x |  1.06x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | 52.3028 ns | 14.9300 ns | 0.8184 ns |     0.94x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | 56.3648 ns | 21.3933 ns | 1.1726 ns |     1.02x |  1.08x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 84.5178 ns | 23.1277 ns | 1.2677 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Loop | FEC&#39;          |         NA |         NA |        NA |         ? |      ? |               ? |            ? |     NA |        NA |
| &#39;Loop | Hyperbee&#39;     | 84.8403 ns | 19.6071 ns | 1.0747 ns |     1.00x |      ? |           1.00x |            ? |      - |         - |
| &#39;Switch | System&#39;     |  4.5583 ns |  7.2686 ns | 0.3984 ns |     1.00x |  2.52x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |  1.8090 ns |  6.3038 ns | 0.3455 ns |     0.40x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |  4.5004 ns |  0.8562 ns | 0.0469 ns |     0.99x |  2.49x |           1.00x |        1.00x |      - |         - |

Benchmarks with issues:
  ExecutionBenchmarks.'Loop | FEC': .NET 9(Runtime=.NET 9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
