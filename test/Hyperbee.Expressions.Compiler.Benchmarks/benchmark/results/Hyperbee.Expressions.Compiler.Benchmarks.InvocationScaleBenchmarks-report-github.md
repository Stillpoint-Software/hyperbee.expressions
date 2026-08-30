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
| &#39;Simple x1000 | System&#39;     | 2.738 μs | 0.0605 μs | 0.0697 μs |     1.00x |  0.89x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | 3.075 μs | 0.0503 μs | 0.0580 μs |     1.12x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | 2.953 μs | 0.1001 μs | 0.1153 μs |     1.08x |  0.96x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | 2.828 μs | 0.0681 μs | 0.0784 μs |     1.00x |  0.75x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | 3.768 μs | 0.1233 μs | 0.1420 μs |     1.33x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | 3.610 μs | 0.0814 μs | 0.0938 μs |     1.28x |  0.96x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | 4.536 μs | 0.0549 μs | 0.0632 μs |     1.00x |  0.88x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | 5.135 μs | 0.1298 μs | 0.1494 μs |     1.13x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | 5.672 μs | 0.1801 μs | 0.2074 μs |     1.25x |  1.10x |           1.00x |        1.00x |         - |
