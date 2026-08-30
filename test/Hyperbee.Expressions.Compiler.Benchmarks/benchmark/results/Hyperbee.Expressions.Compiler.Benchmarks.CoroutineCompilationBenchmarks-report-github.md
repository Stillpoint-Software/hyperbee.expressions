```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  InvocationCount=1  
IterationCount=25  LaunchCount=1  UnrollFactor=1  
WarmupCount=5  

```
| Method                                               | Mean        | Error      | StdDev     | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|----------------------------------------------------- |------------:|-----------:|-----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                            | 2,433.50 μs | 200.856 μs | 268.138 μs |     1.00x |    N/A |           1.00x |          N/A |  65.65 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,663.27 μs | 179.015 μs | 232.770 μs |     0.68x |    N/A |           0.98x |          N/A |  64.05 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,306.68 μs |  57.042 μs |  67.904 μs |       N/A |    N/A |             N/A |          N/A |  63.17 KB |
| &#39;AsyncCapture | System&#39;                              | 2,142.18 μs | 183.817 μs | 239.014 μs |     1.00x |    N/A |           1.00x |          N/A |  66.46 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,404.85 μs |  77.008 μs |  94.573 μs |     0.66x |    N/A |           1.09x |          N/A |  72.59 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,505.74 μs | 111.918 μs | 145.525 μs |     1.00x |    N/A |           1.00x |          N/A |  44.02 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   767.70 μs |  66.555 μs |  86.540 μs |     0.51x |    N/A |           0.83x |          N/A |  36.47 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |   995.38 μs |  44.924 μs |  51.734 μs |       N/A |    N/A |             N/A |          N/A |  42.03 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,590.18 μs | 204.397 μs | 258.496 μs |     1.00x |    N/A |           1.00x |          N/A |  45.17 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   853.35 μs |  54.758 μs |  69.251 μs |     0.54x |    N/A |           0.95x |          N/A |  43.05 KB |
| &#39;NestedClosure | System&#39;                             |   276.87 μs |  29.480 μs |  38.332 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    62.55 μs |   4.667 μs |   5.902 μs |     0.23x |    N/A |           0.47x |          N/A |   3.41 KB |
