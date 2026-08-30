```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                                               | Mean           | Error         | StdDev        | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|----------------------------------------------------- |---------------:|--------------:|--------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 12,223.7678 ns | 1,141.8930 ns | 1,315.0057 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 15,141.9630 ns | 1,548.0387 ns | 1,782.7237 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,148.5516 ns |    27.9577 ns |    32.1961 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     75.6103 ns |     2.3873 ns |     2.7492 ns |     0.07x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     89.5014 ns |     1.9176 ns |     2.1314 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,175.5694 ns |    36.6204 ns |    39.1834 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     88.0721 ns |     4.9136 ns |     5.6585 ns |     0.07x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,167.4224 ns |    65.3315 ns |    75.2359 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     41.0146 ns |     0.8286 ns |     0.9542 ns |     0.04x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |     49.5635 ns |     1.8759 ns |     2.1603 ns |       N/A |    N/A |             N/A |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;                         |  1,101.4274 ns |    25.5283 ns |    29.3984 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     48.9375 ns |     1.3367 ns |     1.3727 ns |     0.04x |    N/A |           0.38x |          N/A | 0.0086 |      72 B |
| &#39;NestedClosure | System&#39;                             |      0.6010 ns |     0.0519 ns |     0.0598 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.4178 ns |     0.0620 ns |     0.0689 ns |     2.36x |    N/A |           1.00x |          N/A |      - |         - |
