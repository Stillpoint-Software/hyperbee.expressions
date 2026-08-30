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
| &#39;AsyncNoCapture | System&#39;                            | 2,456.59 μs | 246.114 μs | 320.017 μs |     1.00x |    N/A |           1.00x |          N/A |  65.34 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,501.94 μs | 134.845 μs | 175.337 μs |     0.61x |    N/A |           0.98x |          N/A |  63.93 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,650.05 μs | 130.293 μs | 173.938 μs |       N/A |    N/A |             N/A |          N/A |  61.65 KB |
| &#39;AsyncCapture | System&#39;                              | 2,258.22 μs | 119.904 μs | 138.081 μs |     1.00x |    N/A |           1.00x |          N/A |  66.01 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,474.40 μs |  91.141 μs | 108.497 μs |     0.65x |    N/A |           1.08x |          N/A |  71.12 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,709.00 μs | 197.148 μs | 263.187 μs |     1.00x |    N/A |           1.00x |          N/A |   43.7 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   819.57 μs |  87.407 μs | 113.654 μs |     0.48x |    N/A |           0.83x |          N/A |  36.37 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,084.78 μs |  83.624 μs |  99.549 μs |       N/A |    N/A |             N/A |          N/A |  41.78 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,667.54 μs | 135.874 μs | 181.388 μs |     1.00x |    N/A |           1.00x |          N/A |  44.85 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   848.17 μs |  77.129 μs | 100.289 μs |     0.51x |    N/A |           0.96x |          N/A |  42.95 KB |
| &#39;NestedClosure | System&#39;                             |   196.87 μs |  11.597 μs |  14.666 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    53.36 μs |   4.301 μs |   5.593 μs |     0.27x |    N/A |           0.48x |          N/A |   3.52 KB |
