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
| &#39;AsyncNoCapture | System&#39;                            | 1,859.78 μs | 125.560 μs | 154.199 μs |     1.00x |    N/A |           1.00x |          N/A |  65.57 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,268.12 μs | 111.755 μs | 149.190 μs |     0.68x |    N/A |           0.98x |          N/A |  64.01 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,370.90 μs |  57.279 μs |  68.187 μs |       N/A |    N/A |             N/A |          N/A |  62.11 KB |
| &#39;AsyncCapture | System&#39;                              | 2,266.51 μs | 146.793 μs | 190.873 μs |     1.00x |    N/A |           1.00x |          N/A |  66.38 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,460.98 μs |  87.442 μs | 116.733 μs |     0.64x |    N/A |           1.07x |          N/A |  71.16 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,557.48 μs |  52.000 μs |  59.884 μs |     1.00x |    N/A |           1.00x |          N/A |  44.02 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   864.48 μs |  50.523 μs |  62.046 μs |     0.56x |    N/A |           0.88x |          N/A |  38.75 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,162.33 μs |  40.262 μs |  49.446 μs |       N/A |    N/A |             N/A |          N/A |  42.03 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,807.16 μs | 132.873 μs | 163.180 μs |     1.00x |    N/A |           1.00x |          N/A |  45.17 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   929.38 μs |  58.202 μs |  73.606 μs |     0.51x |    N/A |           0.98x |          N/A |  44.42 KB |
| &#39;NestedClosure | System&#39;                             |   260.27 μs |  22.080 μs |  28.710 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    66.80 μs |   5.086 μs |   6.246 μs |     0.26x |    N/A |           0.47x |          N/A |   3.41 KB |
