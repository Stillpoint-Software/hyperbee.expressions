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
| &#39;Simple x1000 | System&#39;     | 2.691 μs | 0.1771 μs | 0.0097 μs |     1.00x |  0.83x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | 3.260 μs | 0.5379 μs | 0.0295 μs |     1.21x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | 3.141 μs | 1.7179 μs | 0.0942 μs |     1.17x |  0.96x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | 2.606 μs | 0.3309 μs | 0.0181 μs |     1.00x |  0.76x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | 3.430 μs | 1.7305 μs | 0.0949 μs |     1.32x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | 3.344 μs | 2.4722 μs | 0.1355 μs |     1.28x |  0.97x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | 4.419 μs | 4.7536 μs | 0.2606 μs |     1.00x |  0.93x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | 4.763 μs | 0.8040 μs | 0.0441 μs |     1.08x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | 4.976 μs | 1.5653 μs | 0.0858 μs |     1.13x |  1.04x |           1.00x |        1.00x |         - |
