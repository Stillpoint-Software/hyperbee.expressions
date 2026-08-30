```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                      | Mean     | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|---------------------------- |---------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;Simple x1000 | System&#39;     | 3.253 μs | 0.8820 μs | 0.0483 μs |     1.00x |  0.90x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | 3.633 μs | 0.9470 μs | 0.0519 μs |     1.12x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | 3.599 μs | 0.9028 μs | 0.0495 μs |     1.11x |  0.99x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | 2.727 μs | 0.8853 μs | 0.0485 μs |     1.00x |  0.74x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | 3.667 μs | 1.1975 μs | 0.0656 μs |     1.34x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | 3.960 μs | 2.3202 μs | 0.1272 μs |     1.45x |  1.08x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | 5.087 μs | 2.7131 μs | 0.1487 μs |     1.00x |  0.98x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | 5.170 μs | 1.1261 μs | 0.0617 μs |     1.02x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | 6.656 μs | 2.9854 μs | 0.1636 μs |     1.31x |  1.29x |           1.00x |        1.00x |         - |
