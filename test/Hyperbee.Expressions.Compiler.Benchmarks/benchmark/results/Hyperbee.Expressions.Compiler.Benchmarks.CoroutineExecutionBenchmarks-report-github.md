```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                                               | Mean          | Error         | StdDev        | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|----------------------------------------------------- |--------------:|--------------:|--------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 12,251.439 ns | 1,301.8028 ns | 1,499.1581 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 13,823.026 ns | 1,505.7185 ns | 1,673.6021 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,638.033 ns |   136.6615 ns |   157.3796 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     81.181 ns |     5.3333 ns |     6.1418 ns |     0.05x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     90.102 ns |     2.9377 ns |     3.3830 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,246.902 ns |    55.3432 ns |    63.7333 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     79.711 ns |     1.9401 ns |     2.2342 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,298.597 ns |    78.2476 ns |    90.1100 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     50.273 ns |     1.3710 ns |     1.5788 ns |     0.04x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;                         |  1,190.318 ns |    67.5135 ns |    77.7486 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     59.822 ns |     2.3159 ns |     2.6670 ns |     0.05x |    N/A |           0.38x |          N/A | 0.0086 |      72 B |
| &#39;NestedClosure | System&#39;                             |      1.582 ns |     0.1500 ns |     0.1605 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.244 ns |     0.1046 ns |     0.1204 ns |     0.79x |    N/A |           1.00x |          N/A |      - |         - |
