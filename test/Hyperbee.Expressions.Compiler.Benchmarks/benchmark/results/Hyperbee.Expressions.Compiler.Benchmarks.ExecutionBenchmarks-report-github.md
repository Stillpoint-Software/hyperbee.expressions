```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                | Mean       | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|---------------------- |-----------:|----------:|----------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;Simple | System&#39;     |  1.0607 ns | 0.1163 ns | 0.1339 ns |     1.00x |  0.56x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        |  1.8778 ns | 0.0827 ns | 0.0953 ns |     1.77x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   |  1.8345 ns | 0.0951 ns | 0.1057 ns |     1.73x |  0.98x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    |  0.7688 ns | 0.1456 ns | 0.1558 ns |     1.00x |  0.65x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       |  1.1792 ns | 0.0868 ns | 0.0999 ns |     1.53x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  |  1.4690 ns | 0.0656 ns | 0.0756 ns |     1.91x |  1.25x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   |  1.2524 ns | 0.0771 ns | 0.0888 ns |     1.00x |  0.84x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      |  1.4994 ns | 0.0842 ns | 0.0969 ns |     1.20x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; |  1.9494 ns | 0.1752 ns | 0.2017 ns |     1.56x |  1.30x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | 63.0066 ns | 2.2651 ns | 2.6085 ns |     1.00x |  1.14x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | 55.0788 ns | 1.6051 ns | 1.7174 ns |     0.87x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | 54.7906 ns | 1.2848 ns | 1.4796 ns |     0.87x |  0.99x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | 84.4720 ns | 2.3416 ns | 2.4047 ns |     1.00x |  0.93x |           1.00x |        1.00x |      - |         - |
| &#39;Loop | FEC&#39;          | 90.4031 ns | 1.5638 ns | 1.6732 ns |     1.07x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Loop | Hyperbee&#39;     | 84.1498 ns | 1.2071 ns | 1.3417 ns |     1.00x |  0.93x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | System&#39;     |  3.5165 ns | 0.2887 ns | 0.3325 ns |     1.00x |  0.95x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        |  3.7132 ns | 0.3399 ns | 0.3914 ns |     1.06x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   |  3.3867 ns | 0.1666 ns | 0.1919 ns |     0.96x |  0.91x |           1.00x |        1.00x |      - |         - |
