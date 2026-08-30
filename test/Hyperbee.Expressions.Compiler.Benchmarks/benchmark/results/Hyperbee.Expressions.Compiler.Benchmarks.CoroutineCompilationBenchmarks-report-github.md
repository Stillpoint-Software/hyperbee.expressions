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
| &#39;AsyncNoCapture | System&#39;                            | 2,036.51 μs | 184.192 μs | 239.502 μs |     1.00x |    N/A |           1.00x |          N/A |  65.57 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | 1,173.68 μs |  79.299 μs | 105.862 μs |     0.58x |    N/A |           0.98x |          N/A |  64.09 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | 1,652.58 μs | 190.329 μs | 254.083 μs |       N/A |    N/A |             N/A |          N/A |   61.9 KB |
| &#39;AsyncCapture | System&#39;                              | 2,389.17 μs | 229.995 μs | 307.036 μs |     1.00x |    N/A |           1.00x |          N/A |  66.24 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | 1,509.13 μs | 135.401 μs | 171.239 μs |     0.63x |    N/A |           1.12x |          N/A |  74.27 KB |
| &#39;EnumerableNoCapture | System&#39;                       | 1,641.53 μs | 187.562 μs | 230.343 μs |     1.00x |    N/A |           1.00x |          N/A |  44.02 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |   690.56 μs |  38.332 μs |  45.631 μs |     0.42x |    N/A |           0.83x |          N/A |  36.55 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,032.49 μs |  94.358 μs | 115.880 μs |       N/A |    N/A |             N/A |          N/A |  42.12 KB |
| &#39;EnumerableCapture | System&#39;                         | 1,510.63 μs |  53.969 μs |  62.151 μs |     1.00x |    N/A |           1.00x |          N/A |  45.17 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       |   805.02 μs |  56.804 μs |  73.861 μs |     0.53x |    N/A |           0.96x |          N/A |  43.14 KB |
| &#39;NestedClosure | System&#39;                             |   216.63 μs |  25.171 μs |  33.603 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           |    53.48 μs |   6.083 μs |   7.909 μs |     0.25x |    N/A |           0.48x |          N/A |   3.52 KB |
