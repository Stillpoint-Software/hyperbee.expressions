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
| Method                                          | Mean        | Error      | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|------------------------------------------------ |------------:|-----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                       | 2,339.05 μs | 342.423 μs | 445.25 μs |     1.00x |    N/A |           1.00x |          N/A |  67.09 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                     | 1,421.70 μs | 304.694 μs | 406.76 μs |     0.61x |    N/A |           0.98x |          N/A |  65.79 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,594.63 μs | 271.353 μs | 352.84 μs |       N/A |    N/A |             N/A |          N/A |   63.4 KB |
| &#39;AsyncCapture | System&#39;                         | 2,274.16 μs | 355.171 μs | 461.82 μs |     1.00x |    N/A |           1.00x |          N/A |  67.76 KB |
| &#39;AsyncCapture | Hyperbee&#39;                       | 1,436.55 μs | 223.688 μs | 290.86 μs |     0.63x |    N/A |           1.10x |          N/A |  74.53 KB |
| &#39;EnumerableNoCapture | System&#39;                  | 1,593.21 μs | 220.545 μs | 294.42 μs |     1.00x |    N/A |           1.00x |          N/A |   47.4 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                | 1,024.41 μs | 178.567 μs | 238.38 μs |     0.64x |    N/A |           0.96x |          N/A |  45.54 KB |
| &#39;EnumerableCapture | System&#39;                    | 1,502.95 μs | 153.558 μs | 194.20 μs |     1.00x |    N/A |           1.00x |          N/A |  48.55 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                  | 1,056.40 μs | 171.172 μs | 216.48 μs |     0.70x |    N/A |           1.09x |          N/A |   52.8 KB |
| &#39;NestedClosure | System&#39;                        |   206.96 μs |  28.944 μs |  36.61 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                      |    59.46 μs |   7.715 μs |  10.30 μs |     0.29x |    N/A |           0.48x |          N/A |   3.52 KB |
