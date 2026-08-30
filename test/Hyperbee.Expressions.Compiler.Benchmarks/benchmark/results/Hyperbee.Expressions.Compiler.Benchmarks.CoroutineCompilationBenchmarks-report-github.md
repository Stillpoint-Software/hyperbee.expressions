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
| &#39;AsyncNoCapture | System&#39;                            | 2,271.90 μs | 272.950 μs | 354.912 μs |     1.00x |    N/A |           1.00x |          N/A |  65.52 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,448.61 μs | 161.488 μs | 215.582 μs |     0.64x |    N/A |           0.98x |          N/A |  64.19 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,841.74 μs | 297.570 μs | 386.926 μs |       N/A |    N/A |             N/A |          N/A |  62.02 KB |
| &#39;AsyncCapture | System&#39;                              | 2,463.10 μs | 220.116 μs | 278.377 μs |     1.00x |    N/A |           1.00x |          N/A |  67.59 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,685.20 μs | 178.844 μs | 238.752 μs |     0.68x |    N/A |           1.06x |          N/A |   71.3 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,553.73 μs | 130.858 μs | 174.692 μs |     1.00x |    N/A |           1.00x |          N/A |  42.89 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   708.58 μs |  51.458 μs |  59.259 μs |     0.46x |    N/A |           0.83x |          N/A |  35.55 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,281.19 μs | 100.101 μs | 126.596 μs |       N/A |    N/A |             N/A |          N/A |  40.97 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,696.00 μs | 143.484 μs | 170.807 μs |     1.00x |    N/A |           1.00x |          N/A |  44.04 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   878.14 μs |  82.188 μs | 109.719 μs |     0.52x |    N/A |           0.96x |          N/A |  42.13 KB |
| &#39;NestedClosure | System&#39;                             |   209.16 μs |  16.723 μs |  21.745 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    53.81 μs |   2.894 μs |   3.660 μs |     0.26x |    N/A |           0.48x |          N/A |   3.52 KB |
