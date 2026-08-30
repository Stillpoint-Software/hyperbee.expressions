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
| &#39;AsyncNoCapture | System&#39;                            | 2,139.09 μs | 147.848 μs | 186.980 μs |     1.00x |    N/A |           1.00x |          N/A |  65.52 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,215.37 μs |  88.401 μs | 111.798 μs |     0.57x |    N/A |           0.98x |          N/A |  64.08 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,317.05 μs |  72.972 μs |  84.035 μs |       N/A |    N/A |             N/A |          N/A |  61.84 KB |
| &#39;AsyncCapture | System&#39;                              | 1,993.79 μs | 113.417 μs | 135.014 μs |     1.00x |    N/A |           1.00x |          N/A |  66.27 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,379.10 μs |  86.355 μs | 106.052 μs |     0.69x |    N/A |           1.07x |          N/A |  71.23 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,279.66 μs |  80.582 μs |  95.927 μs |     1.00x |    N/A |           1.00x |          N/A |  42.89 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   651.90 μs |  55.419 μs |  68.059 μs |     0.51x |    N/A |           0.83x |          N/A |  35.55 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,017.18 μs |  43.928 μs |  55.555 μs |       N/A |    N/A |             N/A |          N/A |  40.97 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,580.35 μs | 135.260 μs | 180.569 μs |     1.00x |    N/A |           1.00x |          N/A |  45.98 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   802.00 μs |  49.055 μs |  62.039 μs |     0.51x |    N/A |           0.92x |          N/A |  42.13 KB |
| &#39;NestedClosure | System&#39;                             |   203.20 μs |  13.760 μs |  16.380 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    59.14 μs |   4.500 μs |   5.852 μs |     0.29x |    N/A |           0.48x |          N/A |   3.52 KB |
