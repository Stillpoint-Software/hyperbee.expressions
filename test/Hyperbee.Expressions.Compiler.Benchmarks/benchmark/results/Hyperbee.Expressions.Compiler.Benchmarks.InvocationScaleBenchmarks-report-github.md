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
| &#39;Simple x1000 | System&#39;     | 2.202 μs | 0.0636 μs | 0.0707 μs |     1.00x |  0.74x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | 2.989 μs | 0.0762 μs | 0.0847 μs |     1.36x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | 3.131 μs | 0.0881 μs | 0.1015 μs |     1.42x |  1.05x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | 2.637 μs | 0.0253 μs | 0.0292 μs |     1.00x |  0.74x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | 3.567 μs | 0.1022 μs | 0.1177 μs |     1.35x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | 3.486 μs | 0.0720 μs | 0.0829 μs |     1.32x |  0.98x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | 4.659 μs | 0.1559 μs | 0.1796 μs |     1.00x |  0.87x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | 5.374 μs | 0.3436 μs | 0.3957 μs |     1.15x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | 5.270 μs | 0.0943 μs | 0.1048 μs |     1.13x |  0.98x |           1.00x |        1.00x |         - |
