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
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 12,723.4044 ns |   823.8883 ns |   915.7496 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 11,932.6994 ns | 1,439.4838 ns | 1,599.9824 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |  1,105.0642 ns |    35.3591 ns |    40.7196 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |     80.7806 ns |     8.0337 ns |     8.9295 ns |     0.07x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |     88.8149 ns |     2.9542 ns |     3.4021 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |  1,128.7801 ns |    20.6579 ns |    23.7897 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |     78.3376 ns |     5.4663 ns |     6.2950 ns |     0.07x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |  1,132.7078 ns |    23.8734 ns |    24.5162 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |     35.6174 ns |     0.5670 ns |     0.6529 ns |     0.03x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; |     47.5549 ns |     1.5791 ns |     1.8185 ns |       N/A |    N/A |             N/A |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;                         |  1,099.6825 ns |    41.6603 ns |    46.3053 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |     53.3992 ns |     1.7687 ns |     1.9660 ns |     0.05x |    N/A |           0.38x |          N/A | 0.0086 |      72 B |
| &#39;NestedClosure | System&#39;                             |      0.5213 ns |     0.0780 ns |     0.0898 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |      1.5563 ns |     0.1210 ns |     0.1393 ns |     2.99x |    N/A |           1.00x |          N/A |      - |         - |
