```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  InvocationCount=1  
IterationCount=20  LaunchCount=1  UnrollFactor=1  
WarmupCount=8  

```
| Method                                               | Mean        | Error      | StdDev     | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|----------------------------------------------------- |------------:|-----------:|-----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                            | 1,881.44 μs |  60.779 μs |  67.555 μs |     1.00x |    N/A |           1.00x |          N/A |  65.57 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,173.31 μs |  41.448 μs |  44.348 μs |     0.62x |    N/A |           0.98x |          N/A |  64.01 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,297.24 μs |  44.120 μs |  49.040 μs |       N/A |    N/A |             N/A |          N/A |  61.81 KB |
| &#39;AsyncCapture | System&#39;                              | 2,220.14 μs | 166.757 μs | 192.037 μs |     1.00x |    N/A |           1.00x |          N/A |  90.29 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,284.80 μs |  30.889 μs |  34.333 μs |     0.58x |    N/A |           0.79x |          N/A |  71.16 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,393.37 μs | 108.497 μs | 120.594 μs |     1.00x |    N/A |           1.00x |          N/A |  44.02 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   653.68 μs |  27.795 μs |  30.894 μs |     0.47x |    N/A |           0.83x |          N/A |  36.47 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |   966.27 μs |  36.282 μs |  40.328 μs |       N/A |    N/A |             N/A |          N/A |  44.27 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,522.65 μs |  68.534 μs |  76.175 μs |     1.00x |    N/A |           1.00x |          N/A |  45.17 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   836.14 μs |  33.318 μs |  37.033 μs |     0.55x |    N/A |           0.95x |          N/A |  43.05 KB |
| &#39;NestedClosure | System&#39;                             |   215.50 μs |  18.002 μs |  20.731 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    53.13 μs |   3.787 μs |   4.052 μs |     0.25x |    N/A |           0.47x |          N/A |   3.41 KB |
