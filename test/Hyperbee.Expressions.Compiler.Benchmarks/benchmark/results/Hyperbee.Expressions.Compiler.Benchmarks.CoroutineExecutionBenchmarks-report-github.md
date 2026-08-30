```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                                               | Mean           | Error       | StdDev      | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|----------------------------------------------------- |---------------:|------------:|------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 12,674.8912 ns | 681.6304 ns | 784.9666 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 12,344.0385 ns | 340.6209 ns | 378.5992 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,231.1994 ns |  28.9436 ns |  33.3315 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     94.3996 ns |   2.0093 ns |   2.2334 ns |     0.08x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     99.7258 ns |   7.6052 ns |   8.7582 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,117.2110 ns |  41.5884 ns |  47.8933 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     70.3835 ns |   1.6339 ns |   1.8161 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,086.6600 ns |  46.7781 ns |  53.8698 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     35.8850 ns |   0.2489 ns |   0.2664 ns |     0.03x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |     46.9745 ns |   1.1071 ns |   1.2750 ns |       N/A |    N/A |             N/A |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;                         |  1,109.5802 ns |  28.6965 ns |  33.0469 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     46.3851 ns |   1.2764 ns |   1.4699 ns |     0.04x |    N/A |           0.38x |          N/A | 0.0086 |      72 B |
| &#39;NestedClosure | System&#39;                             |      0.5979 ns |   0.0720 ns |   0.0801 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.4794 ns |   0.0740 ns |   0.0852 ns |     2.47x |    N/A |           1.00x |          N/A |      - |         - |
