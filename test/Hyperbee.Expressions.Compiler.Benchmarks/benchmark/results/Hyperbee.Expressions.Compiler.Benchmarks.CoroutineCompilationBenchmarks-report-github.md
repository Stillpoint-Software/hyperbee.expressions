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
| &#39;AsyncNoCapture | System&#39;                            | 2,048.51 μs |  80.913 μs | 102.329 μs |     1.00x |    N/A |           1.00x |          N/A |  67.13 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,211.79 μs |  67.635 μs |  80.515 μs |     0.59x |    N/A |           0.98x |          N/A |  65.64 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,456.29 μs | 110.423 μs | 147.411 μs |       N/A |    N/A |             N/A |          N/A |   63.4 KB |
| &#39;AsyncCapture | System&#39;                              | 2,120.02 μs | 224.331 μs | 283.707 μs |     1.00x |    N/A |           1.00x |          N/A |  67.76 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,459.28 μs |  55.685 μs |  66.289 μs |     0.69x |    N/A |           1.10x |          N/A |  74.38 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,301.03 μs |  69.927 μs |  83.244 μs |     1.00x |    N/A |           1.00x |          N/A |  43.43 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   667.38 μs |  50.251 μs |  65.341 μs |     0.51x |    N/A |           0.83x |          N/A |  36.09 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |   962.82 μs |  52.387 μs |  64.335 μs |       N/A |    N/A |             N/A |          N/A |  41.51 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,411.08 μs |  80.738 μs |  99.154 μs |     1.00x |    N/A |           1.00x |          N/A |  44.59 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   785.19 μs |  40.045 μs |  49.179 μs |     0.56x |    N/A |           1.02x |          N/A |   45.3 KB |
| &#39;NestedClosure | System&#39;                             |   191.70 μs |  15.811 μs |  19.996 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    55.14 μs |   4.365 μs |   5.828 μs |     0.29x |    N/A |           0.48x |          N/A |   3.52 KB |
