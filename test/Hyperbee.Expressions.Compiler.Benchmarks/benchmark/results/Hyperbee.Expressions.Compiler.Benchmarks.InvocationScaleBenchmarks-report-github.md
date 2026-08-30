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
| &#39;Simple x1000 | System&#39;     | 2.691 μs | 0.7319 μs | 0.0401 μs |     1.00x |  0.77x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | 3.488 μs | 0.9236 μs | 0.0506 μs |     1.30x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | 4.714 μs | 1.9426 μs | 0.1065 μs |     1.75x |  1.35x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | 2.668 μs | 0.5231 μs | 0.0287 μs |     1.00x |  0.69x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | 3.853 μs | 2.2929 μs | 0.1257 μs |     1.44x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | 5.120 μs | 1.1843 μs | 0.0649 μs |     1.92x |  1.33x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | 4.870 μs | 1.3815 μs | 0.0757 μs |     1.00x |  0.96x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | 5.082 μs | 1.8324 μs | 0.1004 μs |     1.04x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | 7.138 μs | 6.6533 μs | 0.3647 μs |     1.47x |  1.40x |           1.00x |        1.00x |         - |
