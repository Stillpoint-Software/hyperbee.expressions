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
| Method                                          | Mean        | Error      | StdDev     | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|------------------------------------------------ |------------:|-----------:|-----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                       | 1,955.11 μs | 216.556 μs | 289.096 μs |     1.00x |    N/A |           1.00x |          N/A |  67.09 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                     | 1,300.09 μs | 123.554 μs | 160.655 μs |     0.66x |    N/A |           0.98x |          N/A |  65.68 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39; | 1,518.23 μs | 222.322 μs | 296.794 μs |       N/A |    N/A |             N/A |          N/A |  63.44 KB |
| &#39;AsyncCapture | System&#39;                         | 1,978.76 μs | 171.654 μs | 217.088 μs |     1.00x |    N/A |           1.00x |          N/A |  67.94 KB |
| &#39;AsyncCapture | Hyperbee&#39;                       | 1,380.66 μs | 153.932 μs | 205.495 μs |     0.70x |    N/A |           1.10x |          N/A |  74.53 KB |
| &#39;EnumerableNoCapture | System&#39;                  | 1,985.71 μs | 288.274 μs | 384.837 μs |     1.00x |    N/A |           1.00x |          N/A |  60.62 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                | 3,597.94 μs | 481.462 μs | 642.738 μs |     1.81x |    N/A |           2.11x |          N/A | 127.78 KB |
| &#39;EnumerableCapture | System&#39;                    | 2,107.71 μs | 190.630 μs | 254.485 μs |     1.00x |    N/A |           1.00x |          N/A |  61.77 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                  | 3,799.90 μs | 746.171 μs | 970.233 μs |     1.80x |    N/A |           2.19x |          N/A | 135.15 KB |
| &#39;NestedClosure | System&#39;                        |   219.05 μs |  15.476 μs |  19.006 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                      |    55.40 μs |   5.142 μs |   6.686 μs |     0.25x |    N/A |           0.48x |          N/A |   3.52 KB |
