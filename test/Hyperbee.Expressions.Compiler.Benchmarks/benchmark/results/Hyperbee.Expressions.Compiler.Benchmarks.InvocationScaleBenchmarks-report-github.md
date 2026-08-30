```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                      | Mean     | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|---------------------------- |---------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;Simple x1000 | System&#39;     | 2.364 μs | 0.1119 μs | 0.1288 μs |     1.00x |  0.79x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | 2.980 μs | 0.1030 μs | 0.1186 μs |     1.26x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | 3.034 μs | 0.0596 μs | 0.0687 μs |     1.28x |  1.02x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | 2.764 μs | 0.0646 μs | 0.0744 μs |     1.00x |  0.74x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | 3.728 μs | 0.2176 μs | 0.2506 μs |     1.35x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | 3.604 μs | 0.0650 μs | 0.0722 μs |     1.30x |  0.97x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | 4.662 μs | 0.1258 μs | 0.1347 μs |     1.00x |  0.93x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | 5.039 μs | 0.0903 μs | 0.1004 μs |     1.08x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | 5.063 μs | 0.1793 μs | 0.2065 μs |     1.09x |  1.00x |           1.00x |        1.00x |         - |
