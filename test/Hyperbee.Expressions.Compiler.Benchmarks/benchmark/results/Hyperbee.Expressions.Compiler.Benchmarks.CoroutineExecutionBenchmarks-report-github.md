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
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 10,368.4452 ns | 798.1209 ns |   887.1093 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 12,018.4924 ns | 938.1627 ns | 1,042.7653 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,328.3143 ns |  41.3055 ns |    47.5675 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     74.9670 ns |   1.8270 ns |     2.1040 ns |     0.06x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     79.0278 ns |   3.1142 ns |     3.5863 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,094.7608 ns |  38.6613 ns |    44.5224 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     75.3991 ns |   3.1095 ns |     3.5809 ns |     0.07x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,161.5119 ns |  32.0141 ns |    35.5836 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     120 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     45.8150 ns |   1.0662 ns |     1.2278 ns |     0.04x |    N/A |           0.47x |          N/A | 0.0067 |      56 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |     46.4758 ns |   0.9842 ns |     1.1334 ns |       N/A |    N/A |             N/A |          N/A | 0.0067 |      56 B |
| &#39;EnumerableCapture | System&#39;                         |  1,144.9349 ns |  45.7086 ns |    52.6381 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     200 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     69.8724 ns |   4.2431 ns |     4.8863 ns |     0.06x |    N/A |           0.40x |          N/A | 0.0095 |      80 B |
| &#39;NestedClosure | System&#39;                             |      0.7550 ns |   0.0549 ns |     0.0632 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.2804 ns |   0.0638 ns |     0.0683 ns |     1.70x |    N/A |           1.00x |          N/A |      - |         - |
