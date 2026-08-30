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
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 12,387.2007 ns | 1,363.7307 ns | 1,459.1766 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 12,087.8967 ns |   597.6373 ns |   688.2400 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,157.9751 ns |    35.7254 ns |    41.1415 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     71.4544 ns |     1.2805 ns |     1.4232 ns |     0.06x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     80.8684 ns |     1.7271 ns |     1.9889 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,089.0589 ns |    24.2184 ns |    27.8899 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     69.3449 ns |     1.5381 ns |     1.5795 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,047.3964 ns |    22.5411 ns |    25.9583 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     120 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     47.2702 ns |     1.3031 ns |     1.5006 ns |     0.05x |    N/A |           0.47x |          N/A | 0.0067 |      56 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |     46.7000 ns |     1.3948 ns |     1.6062 ns |       N/A |    N/A |             N/A |          N/A | 0.0067 |      56 B |
| &#39;EnumerableCapture | System&#39;                         |  1,124.2486 ns |    28.5583 ns |    32.8878 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     200 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     53.5716 ns |     1.4816 ns |     1.7063 ns |     0.05x |    N/A |           0.40x |          N/A | 0.0095 |      80 B |
| &#39;NestedClosure | System&#39;                             |      0.6460 ns |     0.0528 ns |     0.0609 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.5031 ns |     0.0964 ns |     0.1110 ns |     2.33x |    N/A |           1.00x |          N/A |      - |         - |
