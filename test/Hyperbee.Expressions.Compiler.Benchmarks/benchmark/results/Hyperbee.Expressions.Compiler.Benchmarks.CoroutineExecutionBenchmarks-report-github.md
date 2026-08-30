```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                                               | Mean           | Error       | StdDev        | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|----------------------------------------------------- |---------------:|------------:|--------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 13,537.3349 ns | 970.5580 ns | 1,117.6960 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 11,883.7181 ns | 335.0950 ns |   385.8960 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,039.2390 ns |  30.9268 ns |    35.6153 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     73.9682 ns |   4.3807 ns |     5.0448 ns |     0.07x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     93.5703 ns |   4.5062 ns |     5.0086 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,067.5095 ns |  32.1677 ns |    37.0444 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     65.2550 ns |   2.5235 ns |     2.5914 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,065.2656 ns |  50.4851 ns |    58.1387 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     55.5165 ns |   3.7515 ns |     4.1697 ns |     0.05x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;                         |  1,167.7725 ns |  53.8785 ns |    62.0466 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     63.6438 ns |   2.6691 ns |     3.0737 ns |     0.05x |    N/A |           0.38x |          N/A | 0.0086 |      72 B |
| &#39;NestedClosure | System&#39;                             |      0.9866 ns |   0.1616 ns |     0.1861 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.1785 ns |   0.0458 ns |     0.0528 ns |     1.19x |    N/A |           1.00x |          N/A |      - |         - |
