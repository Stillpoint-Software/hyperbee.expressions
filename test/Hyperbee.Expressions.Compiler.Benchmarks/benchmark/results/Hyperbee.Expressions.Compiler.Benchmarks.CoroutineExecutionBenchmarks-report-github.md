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
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 12,718.9743 ns | 1,109.5080 ns | 1,233.2152 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 12,196.7386 ns | 1,113.6473 ns | 1,282.4779 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,095.5880 ns |    29.1474 ns |    33.5662 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     71.8302 ns |     1.4516 ns |     1.6134 ns |     0.07x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     83.6144 ns |     2.9695 ns |     3.4197 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,091.9791 ns |    23.3621 ns |    25.9669 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     70.3695 ns |     1.2780 ns |     1.4717 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,016.4887 ns |    19.6491 ns |    21.8399 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     120 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     54.4536 ns |     2.3445 ns |     2.7000 ns |     0.05x |    N/A |           0.47x |          N/A | 0.0067 |      56 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |     47.6519 ns |     1.0741 ns |     1.1492 ns |       N/A |    N/A |             N/A |          N/A | 0.0067 |      56 B |
| &#39;EnumerableCapture | System&#39;                         |  1,093.3256 ns |    42.6197 ns |    49.0809 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     200 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     56.7825 ns |     1.4001 ns |     1.6124 ns |     0.05x |    N/A |           0.40x |          N/A | 0.0095 |      80 B |
| &#39;NestedClosure | System&#39;                             |      0.8644 ns |     0.1043 ns |     0.1201 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.3118 ns |     0.0494 ns |     0.0549 ns |     1.52x |    N/A |           1.00x |          N/A |      - |         - |
