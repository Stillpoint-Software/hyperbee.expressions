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
| Method                                          | Mean        | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|------------------------------------------------ |------------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                       | 2,833.12 μs | 157.07 μs | 192.89 μs |     1.00x |    N/A |           1.00x |          N/A |  67.09 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                     | 1,781.08 μs |  96.49 μs | 128.81 μs |     0.63x |    N/A |           1.00x |          N/A |  67.02 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,900.12 μs | 142.27 μs | 189.93 μs |       N/A |    N/A |             N/A |          N/A |   63.4 KB |
| &#39;AsyncCapture | System&#39;                         | 2,995.00 μs | 264.84 μs | 344.36 μs |     1.00x |    N/A |           1.00x |          N/A |  67.76 KB |
| &#39;AsyncCapture | Hyperbee&#39;                       | 1,839.10 μs | 121.82 μs | 162.62 μs |     0.61x |    N/A |           1.10x |          N/A |  74.46 KB |
| &#39;EnumerableNoCapture | System&#39;                  | 2,161.62 μs | 233.22 μs | 311.35 μs |     1.00x |    N/A |           1.00x |          N/A |  60.62 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                | 4,385.25 μs | 478.14 μs | 621.72 μs |     2.03x |    N/A |           2.11x |          N/A | 127.78 KB |
| &#39;EnumerableCapture | System&#39;                    | 2,211.80 μs | 185.20 μs | 247.24 μs |     1.00x |    N/A |           1.00x |          N/A |  61.77 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                  | 4,313.86 μs | 362.18 μs | 458.04 μs |     1.95x |    N/A |           2.19x |          N/A | 135.15 KB |
| &#39;NestedClosure | System&#39;                        |   285.42 μs |  28.38 μs |  36.91 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                      |    91.80 μs |  11.88 μs |  15.02 μs |     0.32x |    N/A |           0.48x |          N/A |   3.52 KB |
